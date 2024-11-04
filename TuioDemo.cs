/*
    TUIO C# Demo - part of the reacTIVision project
    Updated with custom menu UI adjustments for C# 7.3 and .NET Framework 4.8
    Only displays directional images for selected menu options, with updated font and layout adjustments.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TUIO;
using System.Text.RegularExpressions;
using System.Diagnostics;

public class TuioDemo : Form, TuioListener
{
    private TuioClient client;
    private SocketClient sClient;
    private Dictionary<long, TuioObject> objectList;
    private Dictionary<long, TuioCursor> cursorList;
    private Dictionary<long, TuioBlob> blobList;
    private List<string> macAddresses = new List<string>();

    public static int width, height;
    private int window_width = 640;
    private int window_height = 480;
    private int screen_width = Screen.PrimaryScreen.Bounds.Width;
    private int screen_height = Screen.PrimaryScreen.Bounds.Height;

    private bool fullscreen = true;
    private bool verbose;
    private int selectedOption = 0;
    private List<string> menuOptions = new List<string> { "Login", "Info", "Fight", "Results" };
    private Font font = new Font("Arial", 24.0f, FontStyle.Bold);  // Updated to a larger, bolder font
    private SolidBrush bgrBrush = new SolidBrush(Color.FromArgb(0, 0, 64));

    private Image boxingImage;
    private Image upArrow, downArrow, leftArrow, rightArrow;
    private bool showMenu = false;

    public TuioDemo(int port)
    {
        verbose = false;

        // Set the window to full screen
        this.WindowState = FormWindowState.Maximized;
        this.FormBorderStyle = FormBorderStyle.None;

        width = screen_width;
        height = screen_height;

        this.ClientSize = new System.Drawing.Size(width, height);
        this.Name = "TuioDemo";
        this.Text = "TuioDemo";

        this.Closing += new CancelEventHandler(Form_Closing);
        this.KeyDown += new KeyEventHandler(Form_KeyDown);

        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.UserPaint |
                      ControlStyles.DoubleBuffer, true);

        objectList = new Dictionary<long, TuioObject>(128);
        cursorList = new Dictionary<long, TuioCursor>(128);
        blobList = new Dictionary<long, TuioBlob>(128);

        client = new TuioClient(port);
        client.addTuioListener(this);
        client.connect();

        // Load images
        boxingImage = Image.FromFile(Path.Combine(Environment.CurrentDirectory, "Boxing.jpeg"));
        upArrow = Image.FromFile(Path.Combine(Environment.CurrentDirectory, "Up.png"));
        downArrow = Image.FromFile(Path.Combine(Environment.CurrentDirectory, "Darrow.png"));
        leftArrow = Image.FromFile(Path.Combine(Environment.CurrentDirectory, "Larrow.png"));
        rightArrow = Image.FromFile(Path.Combine(Environment.CurrentDirectory, "Rarrow.png"));

        Load += async (sender, e) => await initClient();

    }

    private async Task initClient()
    {
        sClient = new SocketClient();

        // Connect to server and start receiving messages
        if (await sClient.connectToSocket("localhost", 5000))
        {
            _ = ReceiveMessages(); // Start receiving messages asynchronously
        }
    }

    private string ExecutePythonScript(string scriptPath)
    {
        try
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "python"; // Ensure Python is in your PATH or provide the full path to python.exe
            start.Arguments = scriptPath; // Pass the script path as an argument
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;

            using (Process process = Process.Start(start))
            {
                // Capture output and errors from the Python script
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    return $"Error: {error}";
                }

                return output;
            }
        }
        catch (Exception ex)
        {
            return $"An error occurred while trying to execute the Python script: {ex.Message}";
        }
    }

    private async Task ReceiveMessages()
    {
        while (true)
        {
            string receivedMessage = await sClient.recieveMessageAsync();
            if (receivedMessage != null)
            {
                ExtractMacAddresses(receivedMessage);
                Console.WriteLine($"Received: {receivedMessage}\r\n"); // Log received MAC address
            }
            await Task.Delay(100); // Small delay to prevent a tight loop
        }
    }

    private void ExtractMacAddresses(string message)
    {
        // Regular expression to match MAC addresses
        var macRegex = new Regex(@"([0-9A-Fa-f]{2}[:]){5}([0-9A-Fa-f]{2})");
        var matches = macRegex.Matches(message);

        foreach (Match match in matches)
        {
            string macAddress = match.Value;
            // Add MAC address to list if it's not already in it
            if (!macAddresses.Contains(macAddress))
            {
                macAddresses.Add(macAddress);
            }
        }
    }

    private void Form_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyData == Keys.F1)
        {
            ToggleFullscreen();
        }
        else if (e.KeyData == Keys.Escape)
        {
            this.Close();
        }
        else if (e.KeyData == Keys.V)
        {
            verbose = !verbose;
        }
    }

    private void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            this.WindowState = FormWindowState.Maximized;
            fullscreen = true;
        }
        else
        {
            this.WindowState = FormWindowState.Normal;
            this.ClientSize = new Size(window_width, window_height);
            fullscreen = false;
        }
    }

    private void Form_Closing(object sender, CancelEventArgs e)
    {
        client.removeTuioListener(this);
        client.disconnect();
        System.Environment.Exit(0);
    }

    public void addTuioObject(TuioObject o)
    {
        lock (objectList)
        {
            objectList.Add(o.SessionID, o);
        }

        if (o.SymbolID == 2) // "Click" action when marker with ID 2 appears
        {
            ShowOptionScreen(menuOptions[selectedOption]);
        }

        if (verbose) Console.WriteLine("add obj " + o.SymbolID + " (" + o.SessionID + ") " + o.X + " " + o.Y + " " + o.Angle);
    }

    public void updateTuioObject(TuioObject o)
    {
        if (o.SymbolID == 1)
        {
            showMenu = true;
            int newSelectedOption = (int)((o.Angle / Math.PI * 180) / (360 / menuOptions.Count)) % menuOptions.Count;

            if (newSelectedOption != selectedOption)
            {
                selectedOption = newSelectedOption;
                Invalidate();
            }
        }
        if (verbose) Console.WriteLine("set obj " + o.SymbolID + " (" + o.SessionID + ") " + o.X + " " + o.Y + " " + o.Angle);
    }

    public void removeTuioObject(TuioObject o)
    {
        lock (objectList)
        {
            objectList.Remove(o.SessionID);
        }
        if (o.SymbolID == 1)
        {
            showMenu = false;
            Invalidate();
        }
        if (verbose) Console.WriteLine("del obj " + o.SymbolID + " (" + o.SessionID + ")");
    }

    public void addTuioCursor(TuioCursor c)
    {
        lock (cursorList)
        {
            cursorList.Add(c.SessionID, c);
        }
        if (verbose) Console.WriteLine("add cursor " + c.SessionID + " " + c.X + " " + c.Y);
    }

    public void updateTuioCursor(TuioCursor c)
    {
        lock (cursorList)
        {
            cursorList[c.SessionID] = c;
        }
        if (verbose) Console.WriteLine("update cursor " + c.SessionID + " " + c.X + " " + c.Y);
    }

    public void removeTuioCursor(TuioCursor c)
    {
        lock (cursorList)
        {
            cursorList.Remove(c.SessionID);
        }
        if (verbose) Console.WriteLine("remove cursor " + c.SessionID);
    }

    public void addTuioBlob(TuioBlob b)
    {
        lock (blobList)
        {
            blobList.Add(b.SessionID, b);
        }
        if (verbose) Console.WriteLine("add blob " + b.SessionID + " " + b.X + " " + b.Y);
    }

    public void updateTuioBlob(TuioBlob b)
    {
        lock (blobList)
        {
            blobList[b.SessionID] = b;
        }
        if (verbose) Console.WriteLine("update blob " + b.SessionID + " " + b.X + " " + b.Y);
    }

    public void removeTuioBlob(TuioBlob b)
    {
        lock (blobList)
        {
            blobList.Remove(b.SessionID);
        }
        if (verbose) Console.WriteLine("remove blob " + b.SessionID);
    }

    public void refresh(TuioTime frameTime)
    {
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;
        g.FillRectangle(bgrBrush, new Rectangle(0, 0, width, height));

        // Display background image
        if (boxingImage != null)
        {
            g.DrawImage(boxingImage, new Rectangle(0, 0, width, height));
        }

        // Draw the menu if ID=1 marker is detected
        if (showMenu)
        {
            DrawMenu(g);
        }
    }

    private void DrawMenu(Graphics g)
    {
        int menuCenterX = width / 2;
        int menuCenterY = height / 2;
        int radius = (int)(Math.Min(width, height) * 0.3); // Make options larger for full screen

        for (int i = 0; i < menuOptions.Count; i++)
        {
            double angle = (i * 2 * Math.PI) / menuOptions.Count;
            int optionX = menuCenterX + (int)(radius * Math.Cos(angle)) - 80; // Adjusted size
            int optionY = menuCenterY + (int)(radius * Math.Sin(angle)) - 40;

            Color optionColor = (i == selectedOption) ? Color.Yellow : Color.LightGray;
            Brush optionBrush = new SolidBrush(optionColor);
            g.FillRectangle(optionBrush, optionX, optionY, 160, 80); // Increased size
            g.DrawString(menuOptions[i], font, Brushes.Black, optionX + 20, optionY + 20); // Centered text
        }

        // Display directional images based on the selected option
        Image directionImage = null;
        if (selectedOption == 3) directionImage = upArrow;      // Use Up.png for "Results"
        else if (selectedOption == 2) directionImage = leftArrow;   // Use Larrow.png for "Fight"
        else if (selectedOption == 0) directionImage = rightArrow;  // Use Rarrow.png for "Login"
        else if (selectedOption == 1) directionImage = downArrow;   // Use Darrow.png for "Info"

        if (directionImage != null)
        {
            g.DrawImage(directionImage, menuCenterX - 40, menuCenterY - 40, 80, 80);
        }
    }

    private void ShowOptionScreen(string option)
    {
        Form optionForm = new Form();
        optionForm.Text = option + " Screen";
        optionForm.Size = new Size(400, 300);
        optionForm.StartPosition = FormStartPosition.CenterScreen;

        Label label = new Label();
        label.Text = "You selected: " + option;
        label.Font = new Font("Arial", 16);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;

        optionForm.Controls.Add(label);

        switch (option)
        {
            case "Login":
                if (macAddresses != null && macAddresses.Count > 0)
                {
                    string firstMacAddress = macAddresses[0];
                    label.Text = $"Logged in as: {firstMacAddress}"; // Display the MAC address
                }
                else
                {
                    label.Text = "No Bluetooth device detected for login.";
                }
                optionForm.ShowDialog();
                break;
            case "Info":
                ShowInfoScreen("Info");
                break;
            case "Fight":
                label.Text += "\n\nPrepare for battle!";
                optionForm.ShowDialog();
                break;
            case "Results":
                label.Text += "\n\nHere are your latest results.";
                optionForm.ShowDialog();
                break;
        }

       
    }

    private void ShowInfoScreen(string option)
    {
        Form optionForm = new Form();
        optionForm.Text = option + " Screen";
        optionForm.Size = new Size(600, 400);
        optionForm.StartPosition = FormStartPosition.CenterScreen;
        optionForm.BackColor = Color.White;

        if (option == "Info")
        {
            // Create a title label
            Label titleLabel = new Label
            {
                Text = "Boxing Guide",
                Font = new Font("Arial", 20, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(10),
                Height = 50
            };

            // Create a panel for content
            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20)
            };

            // Instructions
            string[] instructions = {
                "1. Stance: Keep your feet shoulder-width apart and your knees slightly bent.",
                "2. Arm Position: Keep your fists up, covering your chin and keeping your elbows close.",
                "3. Punching: Rotate your hips and shoulders, extending your arm while snapping your punch.",
                "4. Defense: Keep your movements light and controlled.",
                "Practice regularly and focus on technique to improve over time!"
            };

            // Create a vertical flow layout for instructions
            FlowLayoutPanel instructionsLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false
            };

            foreach (string instruction in instructions)
            {
                Label instructionLabel = new Label
                {
                    Text = instruction,
                    Font = new Font("Arial", 12, FontStyle.Regular),
                    ForeColor = Color.Black,
                    AutoSize = true,
                    Padding = new Padding(0, 10, 0, 10)
                };
                instructionsLayout.Controls.Add(instructionLabel);
            }

            // Add components to the content panel and form
            contentPanel.Controls.Add(instructionsLayout);
            optionForm.Controls.Add(contentPanel);
            optionForm.Controls.Add(titleLabel);
        }
        else
        {
            // Default display for other options
            Label label = new Label
            {
                Text = $"You selected: {option}",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            optionForm.Controls.Add(label);
        }

        optionForm.ShowDialog();
    }

    public class SocketClient
    {
        private NetworkStream stream;
        private TcpClient client;

        public async Task<bool> connectToSocket(string host, int portNumber)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(host, portNumber);
                stream = client.GetStream();
                Console.WriteLine("Connected to server on " + host);
                return true;
            }
            catch (SocketException e)
            {
                Console.WriteLine("Connection Failed: " + e.Message);
                return false;
            }
        }

        public async Task<string> recieveMessageAsync()
        {
            try
            {
                if (stream != null && stream.DataAvailable)
                {
                    byte[] receiveBuffer = new byte[1024];
                    int bytesReceived = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                    if (bytesReceived > 0)
                    {
                        string data = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);
                        return data;
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error receiving message: " + e.Message);
                return null;
            }
        }
        public void CloseConnection()
        {
            stream?.Close();
            client?.Close();
            Console.WriteLine("Connection closed.");
        }
    }

    public static void Main(string[] argv)
    {
        int port = argv.Length > 0 ? int.Parse(argv[0]) : 3333;
        TuioDemo app = new TuioDemo(port);
        Application.Run(app);
    }
}
