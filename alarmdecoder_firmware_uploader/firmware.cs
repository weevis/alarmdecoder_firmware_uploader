/* gmcs -pkg:dotnet amplify.cs */
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;

using System.Configuration;

public class AlarmDecoderFirmware: System.Windows.Forms.Form
{
    private System.Windows.Forms.GroupBox gb;
    private System.Windows.Forms.GroupBox gb2;
	private System.Windows.Forms.GroupBox gb3;
	private System.Windows.Forms.OpenFileDialog ofd;
	private System.Windows.Forms.Button firmware_btn;
    private System.Windows.Forms.Button connect;
    private System.Windows.Forms.Button flash;
    private System.Windows.Forms.ProgressBar progress;
	private System.Windows.Forms.ComboBox port;
	private System.Windows.Forms.Label portLabel;
	private System.Windows.Forms.Label statusLabel;
	private System.Windows.Forms.StatusBar status_bar;
	private System.Windows.Forms.StatusBarPanel status_panel;
	private string[] serialPorts;
	private System.IO.Ports.SerialPort userSerialPort;
	private string serialPortName;
	private int numFirmwareLines;
	private System.IO.StreamReader firmware_stream;
	private bool bFlashing;
	private string config;
	private int baud;
	private System.Threading.ThreadStart childThreadRef;
	private System.Threading.Thread childThread;
	private System.Threading.ThreadStart openThreadRef;
	private System.Threading.Thread openThread;
	private System.Configuration.Configuration settings;
	private string settingsSerialPortName;
	private string firmware_file;

    static public void Main()
    {
        Application.Run(new AlarmDecoderFirmware() );
    }

    public AlarmDecoderFirmware()
    {
		try
		{
			this.settings = ConfigurationManager.OpenExeConfiguration (ConfigurationUserLevel.None);
			this.settingsSerialPortName = settings.AppSettings.Settings ["portName"].Value;
		}
		catch( Exception ex ) 
		{
#if DEBUG
			this.ShowMessageBox ("Unable to read app configuration " + ex.ToString());
#endif
		}

#if DEBUG
		this.baud = 115200;
#else
		this.baud = 115200;
#endif

        Text = "AlarmDecoder Firmware Uploader";

		/* Disable resize of app */
		this.MinimizeBox = false;
		this.MaximizeBox = false;
		this.FormBorderStyle = FormBorderStyle.FixedSingle;

		/* set up menu */
        MenuStrip menu = new MenuStrip();
        menu.Parent = this;

        ToolStripMenuItem file = new ToolStripMenuItem("&File");
        ToolStripMenuItem exit = new ToolStripMenuItem("&Exit", null, new EventHandler(on_exit));
		ToolStripMenuItem help = new ToolStripMenuItem ("&Help");
		ToolStripMenuItem about = new ToolStripMenuItem ("&About", null, new EventHandler (on_about));
        exit.ShortcutKeys = Keys.Control | Keys.X;
        file.DropDownItems.Add(exit);
		help.DropDownItems.Add (about);
		help.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;

        menu.Items.Add(file);
		menu.Items.Add (help);
        MainMenuStrip = menu;

		/* main window size */
        Size = new Size(560, 440);

		/* Set up status bar */
		this.status_bar = new System.Windows.Forms.StatusBar ();
		status_bar.Width = this.Size.Width;
		status_bar.ShowPanels = true;
		this.Controls.Add (status_bar);

		this.status_panel = new System.Windows.Forms.StatusBarPanel ();
		this.status_panel.Width = this.Size.Width;

		string message = "Device: Not Connected";

		status_bar.Panels.Add (this.status_panel);

		this.changeStatusMessage (this.status_bar, message);

		/* Group Box for firmware button */
        this.gb = new System.Windows.Forms.GroupBox();
        this.gb.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.gb.Location = new System.Drawing.Point(18, 32);
        this.gb.Name = "groupbox";
        this.gb.Size = new System.Drawing.Size(250, 100);
        this.gb.TabStop = false;
        this.gb.Text = "Choose Firmware";

		/* Group Box for status */
        this.gb2 = new System.Windows.Forms.GroupBox();
        this.gb2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.gb2.Location = new System.Drawing.Point(290, 32);
        this.gb2.Name = "groupbox2";
        this.gb2.Size = new System.Drawing.Size(250, 100);
        this.gb2.TabStop = false;
        this.gb2.Text = "Current Status";

		this.firmware_btn = new System.Windows.Forms.Button ();
		this.firmware_btn.Click += new EventHandler (open_click);
		this.firmware_btn.Text = "Choose Firmware";
		this.firmware_btn.Location = new System.Drawing.Point (30, 30);
		this.firmware_btn.Size = new System.Drawing.Size (100, 50);

		this.gb.Controls.Add (this.firmware_btn);

		/* Buttons for connect and flash */
        this.connect = new System.Windows.Forms.Button();
        this.connect.Location = new System.Drawing.Point(182, 82);
        this.connect.Text = "Connect";
        this.connect.Size = new System.Drawing.Size(100, 50);
        this.connect.Click += new EventHandler(btn_click);
		this.connect.Name = "connect";

        this.flash = new System.Windows.Forms.Button();
        this.flash.Location = new System.Drawing.Point(300, 82);
        this.flash.Click += new EventHandler(btn_click);
        this.flash.Text = "Flash";
        this.flash.Size = new System.Drawing.Size(100, 50);
		this.flash.Name = "flash";
		this.flash.Enabled = false;

		/* Progress Bar */
        this.progress = new System.Windows.Forms.ProgressBar();
        this.progress.Name = "progressbar";
        this.progress.Size = new System.Drawing.Size(415, 20);
        this.progress.Location = new System.Drawing.Point(32, 32);

		/* Group Box for flashing controls */
		this.gb3 = new System.Windows.Forms.GroupBox ();
		this.gb3.Text = "Control";
		this.gb3.Location = new System.Drawing.Point (38, 170);
		this.gb3.Size = new System.Drawing.Size (480, 200);

		/* Port Selection Combo Box label */
		this.portLabel = new System.Windows.Forms.Label ();
		this.portLabel.Text = "Port";
		this.portLabel.Location = new System.Drawing.Point (32, 82);

		/* Current Status label */
		this.statusLabel = new System.Windows.Forms.Label ();
		this.UpdateText (this.statusLabel, "Not Connected");

		this.statusLabel.Location = new System.Drawing.Point (32, 32);

		this.gb2.Controls.Add (this.statusLabel);

		/* get serial port names */
		this.getPortNames ();

		/* Combo Box to hold serial Port names */
		this.port = new System.Windows.Forms.ComboBox ();
		this.port.SelectionChangeCommitted += new EventHandler (selectionChanged);
		this.port.Items.AddRange (this.getPortNames ());
		this.port.Location = new System.Drawing.Point (32, 100);
		if (this.settingsSerialPortName != null && this.settingsSerialPortName.Length > 0 && this.serialPorts.Length > 0)
			this.port.Text = this.settingsSerialPortName;
		else
			this.port.Text = (this.serialPorts.Length > 0 ? this.serialPorts[0] : "No Ports Found");

		this.config = null;

		if (this.serialPorts.Length == 0)
			this.ShowMessageBox("No serial ports found");

		this.gb3.Controls.Add (this.connect);
		this.gb3.Controls.Add (this.flash);
		this.gb3.Controls.Add (this.progress);
		this.gb3.Controls.Add (this.port);
		this.gb3.Controls.Add (this.portLabel);

        Controls.Add(this.gb);
        Controls.Add(this.gb2);
		Controls.Add (this.gb3);
		CenterToScreen ();
		this.bFlashing = false;
		this.childThreadRef = new System.Threading.ThreadStart (threadFun);
		this.childThread = new System.Threading.Thread (childThreadRef);
		this.openThreadRef = new System.Threading.ThreadStart (openThreadFun);
		this.openThread = new System.Threading.Thread (openThreadRef);
    }


	private void selectionChanged(object sender, EventArgs e)
	{
		ComboBox c = sender as ComboBox;
		string value = c.SelectedItem.ToString ();

		try
		{
			this.settings.AppSettings.Settings.Remove ("portName");
			this.settings.AppSettings.Settings.Add ("portName", value);

			this.settings.Save (ConfigurationSaveMode.Modified);
			ConfigurationManager.RefreshSection ("appSettings");
		}
		catch( Exception ex ) 
		{
#if DEBUG
			Console.WriteLine("Could not write configuration file: " + ex.ToString());
#endif
		}
	}

	/* Get list of serial ports */
	private string[] getPortNames()
	{
		this.serialPorts = System.IO.Ports.SerialPort.GetPortNames ();
		return this.serialPorts;
	}

	/* Open firmware resource, count number of firmware lines */
	private int openFirmwareStream()
	{
		string line = "";
		int i = 0;
		try
		{
			if( this.firmware_file != null )
			{
				this.firmware_stream = File.OpenText(this.firmware_file);
			}
			else
				return -1;

			while( (line = this.firmware_stream.ReadLine()) != null )
			{
				if( line.Length > 0 )
				{
					if( line[0] == ':' )
						i++;
				}
			}
			this.numFirmwareLines = i;
			this.firmware_stream.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
			this.firmware_stream.DiscardBufferedData();
		}
		catch( Exception ex ) 
		{
#if DEBUG
			Console.WriteLine (ex.ToString ());
#endif
			return -1;
		}
		return 0;
	}

	private void open_click(object sender, EventArgs e)
	{
		this.ofd = new OpenFileDialog ();

		this.ofd.InitialDirectory = "c:\\";
		this.ofd.Filter = "Hex Files (*.hex)|*.hex";
		this.ofd.FilterIndex = 2;
		this.ofd.RestoreDirectory = true;

		if (this.ofd.ShowDialog () == DialogResult.OK) 
		{
			this.firmware_file = this.ofd.FileName;
		}
	}

	/* Button Click event */
    private void btn_click(object sender, EventArgs e)
    {
		this.serialPortName = this.port.Text;
		var b = sender as System.Windows.Forms.Button;
		if (b.Name == "connect") 
		{
			if (this.serialPortName == "No Ports Found") 
			{
				this.ShowMessageBox ("We said there were no serial ports found.");
				this.UpdateEnable (this.flash, false);
			} 
			else 
			{
				if (b.Text == "Disconnect") 
				{
					this.UpdateText (this.connect, "Connect");
					if (this.userSerialPort.IsOpen) 
					{
						this.UpdateText (this.statusLabel, "Not Connected");
						this.userSerialPort.Close ();
						this.userSerialPort = null;
					}
					this.UpdateText (this.statusLabel, "Not Connected");
					string message = "Device: Not Connected";
					this.changeStatusMessage (this.status_bar, message);
					this.UpdateEnable (this.flash, false);
					this.UpdateProgress (this.progress, 0, Color.Green);
				} 
				else
				{
					this.openThread = new System.Threading.Thread (openThreadRef);
					this.openThread.Start ();
				}
			}
		}
		if (b.Name == "flash") 
		{
			DialogResult dr = MessageBox.Show ("Are you sure you want to flash the firmware?", "Confirmation", MessageBoxButtons.YesNo);

			if (dr == DialogResult.Yes) 
			{
				if (this.userSerialPort.IsOpen) 
				{
					this.UpdateEnable (this.flash, false);
					this.UpdateEnable (this.connect, false);
					this.UpdateText (this.statusLabel, "Flashing...");
					string message = "Device: " + this.serialPortName + " " + this.baud.ToString() + " -- Flashing...";
					this.changeStatusMessage (this.status_bar, message);
					this.bFlashing = true;
					this.childThread = new System.Threading.Thread (childThreadRef);
					this.childThread.Start ();
				} 
				else 
				{
					this.ShowMessageBox ("Some reason the serial port became closed. Reconnect.");
					this.UpdateEnable (this.flash, false);
				}
			}
		}
    }
	
	public delegate void UpdateControlEnable (Control ctrl, bool b);
	public delegate void UpdateTextCallback(Control ctrl, string message);
	public delegate void UpdateStatusCallback(Control ctrl, string message);
	public delegate void UpdateProgressCallback (Control ctrl, int value, Color color);
	public delegate void ThreadSafeMessageBox (string text);
	public delegate DialogResult ThreadSafeDialog (Form parent, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon);

	public DialogResult ShowDialog(Form parent, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
	{
		if (parent.InvokeRequired) 
		{
				return (DialogResult)parent.Invoke (new ThreadSafeDialog (ShowDialog), parent, text, caption, buttons, icon);
		}

		return MessageBox.Show (text, caption, buttons, icon);
	}

	/* Update progress bar */
	private void UpdateProgress (Control ctrl, int value, Color color)
	{
		if (ctrl.InvokeRequired) 
		{
			ctrl.Invoke(new UpdateProgressCallback(UpdateProgress), new object[] { ctrl, value, color });
		} 
		else 
		{
			this.progress.Value = value;
			this.progress.ForeColor = color;
			this.progress.Update ();
		}
	}

	/* Thread safe message box */
	private void ShowMessageBox(string text)
	{
		if (this.InvokeRequired) 
		{
			this.Invoke (new ThreadSafeMessageBox(ShowMessageBox), new object[] { text });
		} 
		else
		{
			MessageBox.Show (text);
		}
	}

	/* Enable/Disable a control */
	public void UpdateEnable( Control ctrl, bool b)
	{
		if (ctrl.InvokeRequired) 
		{
			ctrl.Invoke (new UpdateControlEnable (UpdateEnable), new object[] { ctrl, b });
		} 
		else
		{
			ctrl.Enabled = b;
		}
	}

	/* Update Text on a control */
	public void UpdateText( Control control, string message )
	{
		if (control.InvokeRequired) 
		{
			control.Invoke (new UpdateTextCallback (UpdateText), new object[] { control, message });
		} 
		else
		{
			control.Text = message;
		}
	}

	/* Delegate for updating status bar text */
	private void changeStatusMessage(Control ctrl, string message)
	{
		if (ctrl.InvokeRequired) 
		{
			ctrl.Invoke (new UpdateStatusCallback (changeStatusMessage), new object[] { ctrl, message });
		} 
		else 
		{
			this.status_panel.Text = message;
		}
	}

	private int checkSerialNumber()
	{
		this.changeStatusMessage(this.status_bar, "Checking serial number...");
		this.Write("=\r\n");
		if( this.WaitForPattern("!sn:") != 0 )
		{
			//this.ShowMessageBox("Serial Number not detected...");
			return -1;
		}
		return 0;
	}

	private void Approved()
	{
		string message;

		this.UpdateText(this.statusLabel, "Connected");
		this.UpdateEnable(this.flash, true);
		DialogResult dr = this.ShowDialog (this, "Store current device configuration for restoration at end of flash?", "Store Device Config?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
		if (dr == DialogResult.Yes) 
		{
			message = "Device: " + this.serialPortName + " " + this.baud.ToString () + " -- Saving Current Device Configuration";
			this.changeStatusMessage (this.status_bar, message);
			this.Write ("C\r\n");
			if (this.WaitForPattern ("!CONFIG>") == 0) 
			{
				this.config = this.ReadSerialLine ();
			}
		}
		this.UpdateText(this.statusLabel, "Ready to Flash");
			message = "Device: " + this.serialPortName + " " + this.baud.ToString () + " -- Ready to Flash";
		this.changeStatusMessage(this.status_bar, message);
		this.ShowMessageBox("We've detected things properly, please proceed to flash");
	}

	private void openThreadFun()
	{
		try
		{
#if DEBUG
			this.userSerialPort = new SerialPort (this.serialPortName, this.baud, Parity.None, 8, StopBits.One);
#else
			this.userSerialPort = new SerialPort( this.serialPortName, this.baud, Parity.None, 8, StopBits.One );
#endif
			this.userSerialPort.Handshake = Handshake.None;
			this.userSerialPort.ReadTimeout = 10;
			this.userSerialPort.WriteTimeout = 500;
			this.userSerialPort.Open ();
			this.UpdateEnable(this.flash, false);
			this.UpdateText(this.statusLabel, "Connected");
			string message = "Device: " + this.serialPortName + " " + this.baud.ToString() + " -- Connected";
			this.changeStatusMessage(this.status_bar, message);
			this.UpdateText(this.connect, "Disconnect");

			if( this.userSerialPort.IsOpen )
			{
				this.Approved();
			}
			else
				this.ShowMessageBox("Not Open Port");
		}
		catch( System.IO.IOException ex )
		{
			this.ShowMessageBox("Unable to Open Port: " + ex.ToString());
		}
		this.openThread.Abort ();
	}

	/* main thread function - for Fun and profit*/
	private void threadFun()
	{
		bool bFail = false;
		if( this.checkAndUpload() != 0 )
		{
			this.ShowMessageBox ("Failed to flash firmware file...");
			bFail = true;
		}
		this.UpdateEnable (this.flash, true);

		this.bFlashing = false;
		if (bFail == true) 
		{
			this.UpdateText (this.statusLabel, "Failure...");
			string message = "Device: " + this.serialPortName + " " + this.baud.ToString() + " -- Failed to Flash";
			this.changeStatusMessage (this.status_bar, message);
		} 
		else 
		{
			this.UpdateText (this.statusLabel, "Complete...");
			string message = "Device: " + this.serialPortName + " " + this.baud.ToString() + " -- Flash Complete";
			this.changeStatusMessage (this.status_bar, message);
			if (this.config != null) 
			{
				DialogResult dr = this.ShowDialog (this, "Restore saved configuration values?", "Device Configuration", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

				if( dr == DialogResult.Yes )
					this.Write ("C" + this.config + "\r\n");
			}

			this.ShowMessageBox ("Firmware Flash Complete...");
		}
		this.UpdateEnable (this.connect, true);
		this.childThread.Abort ();
	}

	/* Decrypt resource, Get into bootloader, flash firmware */
	private int checkAndUpload()
	{
		int lineCount = 0;
		string message = "Decrypting firmware stream...";
		this.changeStatusMessage (this.status_bar, message);
		if (this.openFirmwareStream () != 0)
			return 1;

		message = "Getting into bootloader...";
		this.changeStatusMessage (this.status_bar, message);
		if (this.Write ("=\r\n") != 0)
			return 1;

		message = "Waiting for bootloader...";
		this.changeStatusMessage (this.status_bar, message);
		if (this.WaitForPattern ("!boot") != 0)
			return 1;

		message = "Interrupting bootloader...";
		this.changeStatusMessage (this.status_bar, message);
		if (this.Write ("=\r\n") != 0)
			return 1;

		message = "Waiting for load prompt...";
		this.changeStatusMessage (this.status_bar, message);
		if (this.WaitForPattern ("!load") != 0)
			return 1;

		string line = " ";

		System.Threading.Thread.Sleep (500);
		this.UpdateProgress (this.progress, 0, Color.Green);

		message = "Device: " + this.serialPortName + " " + this.baud.ToString() + " -- Flashing...";
		this.changeStatusMessage (this.status_bar, message);
		int value = 0;
		DateTime start;
		while (!this.firmware_stream.EndOfStream) 
		{
			line = this.firmware_stream.ReadLine ();
#if DEBUG
			Console.WriteLine (">" + line);
#endif
			if (line.Length > 0) 
			{
				if (line [0] == ':') 
				{
					lineCount++;
					value = (100 * lineCount) / this.numFirmwareLines;
					this.Write (line + "\r");
					start = DateTime.Now;
					while (this.userSerialPort.BytesToRead == 0) 
					{
						if ((DateTime.Now - start).Seconds > 3) 
						{
							this.UpdateProgress (this.progress, value, Color.Red);
							this.ShowMessageBox ("Timeout waiting for read");
							return 1;
						}
						continue;
					}

					line = this.ReadSerialLine ();
					if (line.Length > 0) 
					{
#if DEBUG
						Console.WriteLine ("<" + line);
#endif
						if (line.StartsWith ("!ce")) 
						{
							this.UpdateProgress (this.progress, value, Color.Red);
							this.ShowMessageBox ("Checksum Error");
							return 1;
						}
						if (line.StartsWith ("!no")) 
						{
							this.UpdateProgress (this.progress, value, Color.Red);
							this.ShowMessageBox ("Invalid data sent to bootloader");
							return 1;
						}
						if (line.StartsWith ("!ok")) 
						{
							this.UpdateProgress (this.progress, value, Color.Green);
							return 0;
						}

					}
					else
					{
						if (lineCount > 1) 
						{
							this.UpdateProgress (this.progress, value, Color.Red);
							this.ShowMessageBox ("Did not get response from firmware write");
							//this.progress.ForeColor = Color.Red;
							return 1;
						}
					}

					this.UpdateProgress (this.progress, value, Color.Green);
					System.Threading.Thread.Sleep (100);
				}
			}
		}
		start = DateTime.Now;
		while (this.userSerialPort.BytesToRead == 0) 
		{
			if ((DateTime.Now - start).Seconds > 3) 
			{
				this.ShowMessageBox ("Timeout waiting for read");
				return 1;
			}
			continue;
		}

		line = this.ReadSerialLine ();
		if (line.Length > 0) 
		{
			if (line.StartsWith ("!ok")) 
			{
				return 0;
			}
		}
		return 1;
	}

	/* App Close menu event */
    void on_exit( object sender, EventArgs e)
    {
		if (this.bFlashing == false) 
		{
			if (this.firmware_stream != null) 
			{
				this.firmware_stream.Dispose ();
			}

			if (this.userSerialPort != null) 
			{
				if (this.userSerialPort.IsOpen)
					this.userSerialPort.Close ();
			}
			Close ();
		} 
		else
			this.ShowMessageBox("In the middle of flashing, you shouldn't do that");
    }

	/* About menu event */
	void on_about(object sender, EventArgs e)
	{
		this.ShowMessageBox ("AlarmDecoder Firmware Updater");
	}

	/* Read a line from serial, bail after 3seconds or complete line */
	private string ReadSerialLine()
	{
		bool done = false;
		int dataIn;
		string line = "";

		DateTime start = DateTime.Now;
		while (!done && (DateTime.Now - start).Seconds < 3) 
		{
			dataIn = this.ReadSerialByte ();

			if (dataIn != -1) 
			{
				if (dataIn == 0x0a) //LF
				{ 
					done = true;
				}
				else
				{
					if (dataIn != 0x0d) 
					{
						line += (char)dataIn;
					}
				}
			}
		}
		if (!done) 
		{
#if DEBUG
			Console.WriteLine ("Failed to read serial line");
#endif
		}
		return line;
	}

	/* Read a byte from the serial stream */
	private int ReadSerialByte()
	{
		int b = -1;

		if (this.userSerialPort.IsOpen) 
		{
			if (this.userSerialPort.BytesToRead == 0) 
			{
				System.Threading.Thread.Sleep (10);
				return -1;
			}
			try
			{
				b = this.userSerialPort.ReadByte();
			}
			catch( TimeoutException ex ) 
			{
#if DEBUG
				Console.WriteLine ("Serial Timeout: " + ex.ToString ());
#endif
			}
		}

		return b;
	}

	/* Write to the serial port */
	private int Write(string keys)
	{
		if (this.userSerialPort != null) 
		{
			if (this.userSerialPort.IsOpen) 
			{
				this.userSerialPort.Write (keys);
				return 0;
			}
			else
				return 1;
		}
		else
			return 1;
	}

	/* Wait for a specific string to come through the serial port, or bail after 15s of not finding it */
	private int WaitForPattern(string pattern )
	{
		int pos = 0;
		int mychar;
		DateTime start = DateTime.Now;
		while (true) 
		{
			mychar = this.ReadSerialByte ();
			if (mychar != -1) 
			{
				if (mychar == pattern [pos]) 
				{
					if (++pos == pattern.Length)
						return 0;
				} 
				else 
				{
					pos = 0;
				}

			}

			if ((DateTime.Now - start).Seconds > 15)
				return -1;
		}
	}		
}
