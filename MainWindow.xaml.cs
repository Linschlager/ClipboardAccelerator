﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.ComponentModel;
using System.Xml.Linq;
using System.IO.Pipes;
using System.Management.Automation;
using System.Collections.ObjectModel;

namespace ClipboardAccelerator
{
    public partial class MainWindow : Window
    {
        // Define replacement placeholder constants for parsing the XML file
        private const string ClipboardArgumentString = "%%ca**";
        private const string OptionalArgumentString = "%%oa**";
        private const string PipeNameString = "%%pn**";
        /* OLD:
        ClipboardArgumentString = "%%**";
        OptionalArgumentString = "$$**";
        PipeNameString = "$$pn**";
        */


        // Sent when the contents of the clipboard is updated
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        // Create the List of ClipboardEntries
        private List<ClipboardEntry> lClipboardList = new List<ClipboardEntry>();

        // Create a timer variable to limit the clipboard access
        private System.Timers.Timer ClipboardTimer;

        // Class global list of "FileItem" objects
        private List<FileItem> FileItems = new List<FileItem>();

        // Class global list of "RegExpConf" objects
        private List<RegExpConf> RegExpItems = new List<RegExpConf>();

        // Class global flag to indicate if the color of the listbox has been changed (regex in function tbClipboardContent_TextChanged)
        bool bListBoxColorSet = false;

        // Close flag for testing
        bool bTestingCloseFlag = false;


        // TODO: Initialize all settings ( Properties.Settings.Default.* ) with its default values
        


        public MainWindow()
        {
            // TODO: Implement a "semaphore" or "lock" to prevent a second start in the below function
            CheckIfAlreadyRunning(); 

            InitializeComponent();            

            // Populate the RegExItems list with the RegExp config
            GetRegExConfig();

            // Remove Clipboard hook when main windows / application is closing
            // Source: http://stackoverflow.com/questions/3683450/handling-the-window-closing-event-with-wpf-mvvm-light-toolkit
            //Closing += ClipboardHook.OnWindowClosing;
            Closing += MainWindow_Closing;

            MessageBoxResult result = MessageBox.Show("This version of Clipboard Accelerator is for testing purposes only. Executing external commands can harm your computer and data.\n\nUse at your own risk.\n\nDo not distribute this version.\n\nClick \"Yes\" to agree, \"No\" to close Clipboard Accelerator.", "Clipboard Accelerator", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No)
            {
                bTestingCloseFlag = true;
                this.Close();
            }
            
        }


        void CheckIfAlreadyRunning()
        {
            int OwnProcessID = Process.GetCurrentProcess().Id;
            Process[] Processes = Process.GetProcesses();

            // Source: http://stackoverflow.com/questions/52797/how-do-i-get-the-path-of-the-assembly-the-code-is-in
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path.Replace("/", "\\"));
            Logger.WriteLog("Own instance: " + path + " Own process ID: " + OwnProcessID);
            
            foreach (Process p in Processes)
            {
                try
                {
                    if (p.Id == OwnProcessID) continue;

                    if(p.MainModule.FileName == path)
                    {
                        MessageBox.Show("Clipboard Accelerator is already running: " + Environment.NewLine + p.MainModule.FileName + " ID: " + p.Id.ToString(), "Note", MessageBoxButton.OK, MessageBoxImage.Asterisk);

                        // Source: http://stackoverflow.com/questions/7146080/closing-applications
                        System.Environment.Exit(1);
                    }                    
                }
                catch(Exception) { }
            }
        }


        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(!bTestingCloseFlag)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to close Clipboard Accelerator?", "Clipboard Accelerator", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;                    
                    return;
                }
            }            

            ClipboardHook.OnWindowClosing();            
        }


        // Hook the WinProg procedure
        // Source: https://pingfu.net/csharp/2015/04/22/receive-wndproc-messages-in-wpf.html
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
                       
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }

            // Install the Clipboard notification hook  
            HwndSource hwndSourceHandle = PresentationSource.FromVisual(this) as HwndSource;
            ClipboardHook.InstallHook(hwndSourceHandle.Handle);


            // Create the actual instance of the Clipboard timer
            // Source: http://stackoverflow.com/questions/12535722/what-is-the-best-way-to-implement-a-timer
            ClipboardTimer = new System.Timers.Timer();


            // Set the text size of the listbox of external commands to the size stored in the user settings
            if (Properties.Settings.Default.uiCommandsFontSize > 7 && Properties.Settings.Default.uiCommandsFontSize < 73)
            {                
                listBoxCommands.FontSize = Properties.Settings.Default.uiCommandsFontSize * 1.33333333;
            }
            else
            {   // TODO: set correct default font size   
                listBoxCommands.FontSize = 22 * 1.33333333;
            }

            Logger.WriteLog("Program started.");
        }


        // Handle the WinProg messages
        // NOTE: removed "static" from method "WinProc" to make GUI update "tbClipboardContent.Text = ..." work
        // Source: https://pingfu.net/csharp/2015/04/22/receive-wndproc-messages-in-wpf.html
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if(msg == WM_CLIPBOARDUPDATE)
            {
                if (cBIgnoreCBUpdate.IsChecked.Value) { return IntPtr.Zero; };
                

                try
                {
                    // Get Data from Clipboard
                    // Source: http://www.fluxbytes.com/csharp/how-to-monitor-for-clipboard-changes-using-addclipboardformatlistener/
                    IDataObject iData = Clipboard.GetDataObject();      // Clipboard's data.

                    // Todo: Send picture data in clipboard directly to mspaint
                    //if (iData.GetDataPresent(DataFormats.Bitmap)) { MessageBox.Show("Bitmap on clipboard");  }

                    if (iData.GetDataPresent(DataFormats.Text))
                    {
                        // Only copy the data from the clipboard if the timer is NOT running. E.g. to prevent multiple clipboard updates by applications like Excel which push the same data in multiple formats to the clipboard
                        if (ClipboardTimer.Enabled == false)
                        {
                            // Make sure the clipboard access delay is between 0 and 10 seconds
                            // TODO: document delay is between 0 and 10 seconds & recommended = 0.5 seconds
                            if(Properties.Settings.Default.dClipboardDelay >= 0 && Properties.Settings.Default.dClipboardDelay < 10001)
                            {
                                ClipboardTimer.Interval = Properties.Settings.Default.dClipboardDelay;
                            }
                            else
                            {
                                // Default clipboard delay is 0.5 seconds
                                ClipboardTimer.Interval = 500;                                
                            }
                            
                            ClipboardTimer.AutoReset = false;
                            ClipboardTimer.Enabled = true;

                            // Todo: Setup logic to set user defined limit of CB lenth
                            StringBuilder text = new StringBuilder();
                            int iCBTextLenth = 0;

                            text.Append(iData.GetData(DataFormats.Text));

                           
                            // TODO: Make sure that the (int) cast below does not introduce a bug, e.g. uiClipDisplaySize could be to big to fit into a regular "int"
                            // INT check done in "SettingsWindow.xaml.cs" - TODO: change uint to int so that it is not possible to change in the config file manually
                            if (text.Length > Properties.Settings.Default.uiClipDisplaySize) iCBTextLenth = (int)Properties.Settings.Default.uiClipDisplaySize;
                            else iCBTextLenth = text.Length;

                            
                            // Combine clipboards if checkbox is checked
                            // Todo: Enable clipboard history functionality <-- check if this makes sense
                            if (cBCombineClipboard.IsChecked.Value)
                            {
                                if(tbClipboardContent.LineCount == 1)
                                {
                                    if(tbClipboardContent.GetLineLength(0) == 0)
                                    {
                                        tbClipboardContent.AppendText(text.ToString(0, iCBTextLenth));
                                    }
                                    else
                                    {
                                        tbClipboardContent.AppendText(Environment.NewLine + text.ToString(0, iCBTextLenth));
                                    }
                                }
                                else
                                {
                                    tbClipboardContent.AppendText(Environment.NewLine + text.ToString(0, iCBTextLenth));
                                }                                
                            }
                            else
                            {
                                // Compare the captured clipboard to the recent clipboard to prevent multiple clipboards with the same data
                                if (lClipboardList.Count > 0)
                                {
                                    if (text.Equals(lClipboardList[lClipboardList.Count - 1].GetCBTextAsStringBuilder()))
                                    {
					Logger.WriteLog("Ignoring clipboard update because the current clipboard data is identical to the last saved one.");
                                        return IntPtr.Zero;
                                    }
                                }


                                tbClipboardContent.Text = text.ToString(0, iCBTextLenth);


                                lClipboardList.Add(new ClipboardEntry(text));
                                ClipboardEntry.CBInView = lClipboardList.Count - 1;

                                if (lClipboardList.Count > 1) { bPrev.IsEnabled = true; }
                                bNext.IsEnabled = false;
                                bDeleteClipboardEntry.IsEnabled = true;

               
				// Set the clipboard information (time of capture and number of visible clipboard) of the clipboard which was just captured
                                SetCBInfoString();


                                Logger.WriteLog("Captured clipboard: " + lClipboardList.Count.ToString());


                                // Hide the clipboard window if checkbox is checked
                                if (cBHideClipboard.IsChecked.Value) bShowClipboard.Visibility = Visibility.Visible;


                                // Add text in comboOptArg to the list of comboOptArg and clear the text
                                if (comboOptArg.Text != "")
                                {
                                    bool bInList = false;
                                    foreach (var item in comboOptArg.Items)
                                    {
                                        if (item.ToString() == comboOptArg.Text) bInList = true;
                                    }

                                    if (!bInList)
                                    {
                                        comboOptArg.Items.Add(comboOptArg.Text);
                                    }
                                    comboOptArg.Text = "";
                                }                                
                            }
                            

                            // Show the clipboard changed notification window
                            // Source: http://stackoverflow.com/questions/7373335/how-to-open-a-child-windows-under-parent-window-on-menu-item-click-in-wpf
                            if (NotificationWindow.bIsFirstWindow == true && cBShowNW.IsChecked.Value)
                            {
                                NotificationWindow nw = new NotificationWindow();
                                nw.ShowInTaskbar = false;
				nw.Left = Properties.Settings.Default.dXNotifyWindow; // Todo: Check this value - make sure it is in a valid range. E.g. 0 -> max screen size
                                nw.Top = Properties.Settings.Default.dYNotifyWindow; // Todo: Check this value - make sure it is in a valid range. E.g. 0 -> max screen size
                                nw.Topmost = true;
                                nw.Owner = Application.Current.MainWindow;
                                nw.Show();
                            }
                        }
                        // Clipboard timer is running -> no data will be copied from clipboard
                        else
                        {
                            Logger.WriteLog("Ignoring clipboard update because of the clipboard access delay (see Advanced Settings for details).");
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.WriteLog("Failed to get data from Clipboard. Error: " + e.Message);
                    return IntPtr.Zero;
                }
            }
            return IntPtr.Zero;
        }


        // Log / Debug window
        private void buttonRun_Click(object sender, RoutedEventArgs e)
        {
            // Allow only one Debug window at the same time
            // dw.ShowDialog();
            if (Debug.IsFirstWindow)
            {
                // Init the debug window
                Debug dw = new Debug();
                //dw.Owner = Application.Current.MainWindow; <-- this would make the Debug window be always in front of the Main window
                dw.Show();
            }
            // Show the debug window if there is already one open
            else if (Debug.TheDebugWindow != null)
            {
                Debug.TheDebugWindow.WindowState = WindowState.Normal;
                Debug.TheDebugWindow.Activate();
            }


            /* this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Minimized; */
            //this.ShowInTaskbar = false;
            //this.Hide();
        }


        private void listBoxCommands_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Do nothing if no ListBox item was selected
            if (listBoxCommands.SelectedItem == null) { return; } 


            // Do nothing if clipboard window is empty
            if (tbClipboardContent.Text == "") { return; }


            // Split the content in the clipboard window into seperate strings, delimiter = new line. Remove lines with no text.
            string[] saClipboardLines = tbClipboardContent.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        

            switch ( (listBoxCommands.SelectedItem as FileItem).FileExt.ToLower() )
            {
                case ".xml":
                    // Get the path to the selected file
                    string sXmlBatFile = AppDomain.CurrentDomain.BaseDirectory + @"Tools\" + (listBoxCommands.SelectedItem as FileItem).FileName + (listBoxCommands.SelectedItem as FileItem).FileExt;

                    XMLRecord xrec = new XMLRecord(sXmlBatFile);   

                    if (cBFirstLineOnly.IsChecked.Value)
                    {
                        string[] saTheFirstLine = new string[] { saClipboardLines[0] };
                        RunXmlCommand(ref saTheFirstLine, xrec);
                    }
                    else
                    {
                        RunXmlCommand(ref saClipboardLines, xrec);
                    }                                                            
                    break;


                case ".bat":
                case ".cmd":
                case ".ps1":
                    if (cBFirstLineOnly.IsChecked.Value)
                    {
                        RunCommand(saClipboardLines[0]);
                    }
                    else
                    {
                        // Check if more than N lines and show a warning message                        
                        if (tbClipboardContent.LineCount >= Properties.Settings.Default.uiExecutionWarningCount)
                        {
                            MessageBoxResult result = MessageBox.Show("You are about to execute the selected command " + tbClipboardContent.LineCount.ToString() + " times." + Environment.NewLine + Environment.NewLine + "Click Yes to continue.", "Please confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (result == MessageBoxResult.No) { return; }
                        }
                        foreach (string line in saClipboardLines)
                        {                           
                            RunCommand(line);
                        }
                    }
                    break;


                default:
                    MessageBox.Show("File type not supported.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;                                        
            }

        }



        private void RunXmlCommand(ref string[] saLinesToExecute, XMLRecord xrec)
        {
            // Check if more than N lines and show a warning message           
            if (tbClipboardContent.LineCount >= Properties.Settings.Default.uiExecutionWarningCount)
            {
                MessageBoxResult result = MessageBox.Show("You are about to execute the selected command " + tbClipboardContent.LineCount.ToString() + " times." + Environment.NewLine + Environment.NewLine + "Click Yes to continue.", "Please confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No) { return; }
            }
           

            Logger.WriteLog("Data from XML file: \nPath: " + xrec.Path + "\nExecutable: " + xrec.Executable + "\nClass: " + xrec.Class + "\nDescription: " + xrec.Description + "\nStaticArguments: " + xrec.AllArguments);          

            if (xrec.UsePipe == "true")
            {       
                string sAllArguments = "";

                // Create a temporary pipe name
                string sPipeName = Guid.NewGuid().ToString();
                                 
                   
                // Place the optional argument into the arguments string
                // Todo: the below optional argument must be shown in the messagebox which informs the user about the executed command, e.g. set the messagebox call before the Process.Start call and put the sAllArguments variable into the messagebox
                sAllArguments = xrec.AllArguments.Replace(OptionalArgumentString, comboOptArg.Text);

                // Add the pipe name to the command line
                sAllArguments = sAllArguments.Replace(PipeNameString, sPipeName);


                // Get user approval to run the external command
                if (xrec.CommandIsSafe != "true")
                {
                    if (cBNotifyExecution.IsChecked.Value)
                    {
                        // TODO: add note to the message that the command might receive all lines through the pipe
                        MessageBoxResult result = MessageBox.Show("Do you want to run the following external command:" + Environment.NewLine + Environment.NewLine + xrec.Path + @"\" + xrec.Executable + " " + sAllArguments, "Please confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        // + saLinesToExecute[0] + Environment.NewLine + "[+ following lines in the clipboard window]"
                        if (result == MessageBoxResult.No) { return; }
                    }
                }


                // Start the pipe server thread
                Logger.WriteLog("Staring pipe server thread. Pipe name: " + sPipeName);
                StartServer(sPipeName, saLinesToExecute);


                // Setup the startup info object
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = false;
                startInfo.UseShellExecute = false;
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                startInfo.FileName = xrec.Path + @"\" + xrec.Executable;
                startInfo.Arguments = sAllArguments;

                StartExternalProgram(startInfo);
                   

                // Re-enable the "Execute first line only" checkbox
                //if (cbEnableFirstLineOnly.IsChecked.Value) { cBFirstLineOnly.IsChecked = true; }
                if (Properties.Settings.Default.bEnableFirstLineOnly) { cBFirstLineOnly.IsChecked = true; }

            }
            else
            {
                foreach (string sClipboardLine in saLinesToExecute)
                {
                    if (xrec.CommandIsSafe != "true")
                    {
                        if (cBNotifyExecution.IsChecked.Value)
                        {
                            MessageBoxResult result = MessageBox.Show("Do you want to run the external command with the following parameter:" + Environment.NewLine + Environment.NewLine + sClipboardLine, "Please confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (result == MessageBoxResult.No) { return; }
                        }
                    }

                    string sAllArguments = "";                       

                    // Place the main argument into the arguments string                     
                    sAllArguments = xrec.AllArguments.Replace(ClipboardArgumentString, sClipboardLine);                    

                    // Place the optional argument into the arguments string
                    // Todo: the below optional argument must be shown in the messagebox which informs the user about the executed command, e.g. set the messagebox call before the Process.Start call and put the sAllArguments variable into the messagebox
                    sAllArguments = sAllArguments.Replace(OptionalArgumentString, comboOptArg.Text);

                       
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.CreateNoWindow = false;
                    startInfo.UseShellExecute = false;
                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                    startInfo.FileName = xrec.Path + @"\" + xrec.Executable;
                    startInfo.Arguments = sAllArguments;

                    StartExternalProgram(startInfo);


                    // Re-enable the "Execute first line only" checkbox
                    //if (cbEnableFirstLineOnly.IsChecked.Value) { cBFirstLineOnly.IsChecked = true; }
                    if (Properties.Settings.Default.bEnableFirstLineOnly) { cBFirstLineOnly.IsChecked = true; }
                }
            }
        }

        private void RunCommand(string sLineToExecute)
        {
            // TODO: is the below check required? It is already in the double click event function of the listbox
            // Do nothing if no ListBox item was selected
            if (listBoxCommands.SelectedItem == null) { return; }

            string sXmlBatFile = AppDomain.CurrentDomain.BaseDirectory + @"Tools\" + (listBoxCommands.SelectedItem as FileItem).FileName + (listBoxCommands.SelectedItem as FileItem).FileExt;            


            // TODO: check if it makes sense to make the below "if" a switch statement - e.g. to reduce code duplicates
            // File is a BAT or CMD file            
            if((listBoxCommands.SelectedItem as FileItem).FileExt.ToLower() == ".bat" || (listBoxCommands.SelectedItem as FileItem).FileExt.ToLower() == ".cmd")
            {

                if (cBNotifyExecution.IsChecked.Value)
                {
                    MessageBoxResult result = MessageBox.Show("Do you want to run the external command with the following parameter:" + Environment.NewLine + Environment.NewLine + sLineToExecute, "Please confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) { return; }
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = false;
                startInfo.UseShellExecute = false;
                startInfo.WindowStyle = ProcessWindowStyle.Normal;

                startInfo.FileName = (Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)) + @"\cmd.exe";

                startInfo.Arguments = @"/K " + sXmlBatFile + " " + sLineToExecute;
                
                StartExternalProgram(startInfo);

                
                // Re-enable the "Execute first line only" checkbox
                //if (cbEnableFirstLineOnly.IsChecked.Value) { cBFirstLineOnly.IsChecked = true; }
                if (Properties.Settings.Default.bEnableFirstLineOnly) { cBFirstLineOnly.IsChecked = true; }
            }
            // File is a PS1 file           
            else if ((listBoxCommands.SelectedItem as FileItem).FileExt.ToLower() == ".ps1")
            {
                if (cBNotifyExecution.IsChecked.Value)
                {
                    MessageBoxResult result = MessageBox.Show("Do you want to run the external command with the following parameter:" + Environment.NewLine + Environment.NewLine + sLineToExecute, "Please confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) { return; }
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = false;
                startInfo.UseShellExecute = false;
                startInfo.WindowStyle = ProcessWindowStyle.Normal;

                startInfo.FileName = (Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)) + @"\WindowsPowerShell\v1.0\powershell.exe";

                startInfo.Arguments = @"-NoLogo -NoExit -ExecutionPolicy Bypass -File " + sXmlBatFile + " " + sLineToExecute;
                
                StartExternalProgram(startInfo);
                

                // Re-enable the "Execute first line only" checkbox
                //if (cbEnableFirstLineOnly.IsChecked.Value) { cBFirstLineOnly.IsChecked = true; }
                if (Properties.Settings.Default.bEnableFirstLineOnly) { cBFirstLineOnly.IsChecked = true; }
            }

        }


        private void StartExternalProgram(ProcessStartInfo startInfo)
        {
            Logger.WriteLog("Executing: " + startInfo.FileName + " " + startInfo.Arguments);

            try
            {
                Process exeProcess = Process.Start(startInfo);
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to run external program." + Environment.NewLine + "Error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.WriteLog("Failed to run external program. Error: " + e.Message);
            }
        }



        private void listBoxCommands_Initialized(object sender, EventArgs e)
        {            
            if(GetTools() != 0)
            {
                return;
            }

            // Populate listBoxCommands with the file information
            listBoxCommands.ItemsSource = FileItems;            
        }


        // Update the information about which clipboard is shown and when it was captured
        private void SetCBInfoString()
        {
            string sInfoString = "";


            if (ClipboardEntry.CBInView >= 0)
            {
                sInfoString = "Showing clipboard " + (ClipboardEntry.CBInView + 1).ToString() + " of " + lClipboardList.Count.ToString();
            }            
            
            string sInfoTimeString = ClipboardEntry.CBInView > -1 ? lClipboardList[ClipboardEntry.CBInView].sCBTime : "";

            tBCBInfoLine.Text = sInfoString;
            tBCBInfoTime.Text = sInfoTimeString;
        }
        


        // Pupulate or update the "FileItems" list with the details of the files and the XML content
        private int GetTools()
        {
            String ExePath = AppDomain.CurrentDomain.BaseDirectory + "Tools";

            // Clear the "FileItems" list to have no duplicates, e.g. if called a second time to refresh the files in the Tools directory
            FileItems.Clear();

            // Source: http://stackoverflow.com/questions/3991933/get-path-for-my-exe
            DirectoryInfo DirInfo = new DirectoryInfo(ExePath);
            try
            {
                FileInfo[] Files = DirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly);
                foreach (FileInfo file in Files)
                {                    
                    // Source of Substring method: http://stackoverflow.com/questions/7356205/remove-file-extension-from-a-file-name-string
                    FileItems.Add(new FileItem() { FileName = file.Name.Substring(0, file.Name.Length - file.Extension.Length), FileExt = file.Extension, ItemBackgroundColor = "", ItemTextColor = "Black" });
                }
            }
            catch (Exception)
            {                
                Logger.WriteLog("Warning: " + ExePath + " not found.");
                MessageBox.Show(ExePath + " not found.", "Warning", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }


            // Populate each FileItem object with the file information and the XML data
            foreach (FileItem fitem in FileItems)
            {
                if (fitem.FileExt.ToLower() == ".xml")
                {
                    string sXmlFile = AppDomain.CurrentDomain.BaseDirectory + @"Tools\" + fitem.FileName + fitem.FileExt;

                    // Read the XML file
                    // Source: http://stackoverflow.com/questions/5604330/xml-parsing-read-a-simple-xml-file-and-retrieve-values
                    XDocument doc;
                    try
                    {
                        doc = XDocument.Load(sXmlFile);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(@"XML file """ + fitem.FileName + fitem.FileExt + @""" contains invalid data." + Environment.NewLine + "Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);                        
                        Logger.WriteLog("XML file contains invalid data: " + ExePath + @"\" + fitem.FileName + fitem.FileExt + Environment.NewLine  + "Error: " + ex.Message);

                        // This sets the FileClass to an empty string in case the XML file has invalid data
                        fitem.FileClass = "";
                        continue;                        
                    }


                    foreach (XElement el in doc.Root.Elements())
                    {
                        string sProgramID = "";
                        string sVisible = "";
                       
                        try
                        {
                            fitem.FileClass = el.Element("class") != null ? el.Element("class").Value : "";
                            sVisible = el.Element("visible") != null ? el.Element("visible").Value : "true";
                            sProgramID = el.Attribute("id") != null ? el.Attribute("id").Value : "Invalid <program> element";
                        }
                        catch
                        {
                            MessageBox.Show(fitem.FileName + " XML file contains invalid data." + Environment.NewLine + "Failed at <program> element: " + sProgramID, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            Logger.WriteLog(fitem.FileName + " XML file contains invalid data. Failed at <program> element: " + sProgramID);
                            return 1;
                        }

                        /* TODO: Not working because when removing a file item from the FileItems list the above "foreach" loop does not work anymore because it runs into an non existing item at the end of the loop
                        if(sVisible.ToLower() != "true")
                        {
                            // TODO: Replace the below line -> add a new "visible" field in the "fileitem" class to make it aware it is visible so the listbox update can handle this
                            FileItems.Remove(fitem);
                        }     
                        */                                     
                    }
                }
            }
            
            return 0;
        }


        private void buttonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Clipboard Accelerator v. " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + Environment.NewLine + "2017-08-19 C. Paul", "About", MessageBoxButton.OK ,MessageBoxImage.Information);
        }


        // Delete the currently visible Clipboard data
        private void bDeleteClipboardEntry_Click(object sender, RoutedEventArgs e)
        {
            if(ClipboardEntry.CBInView > 0)
            {
                lClipboardList.RemoveAt(ClipboardEntry.CBInView);
                ClipboardEntry.CBInView--;
                tbClipboardContent.Text = lClipboardList[ClipboardEntry.CBInView].GetCBText();
                if (ClipboardEntry.CBInView == 0) { bPrev.IsEnabled = false; }
                if (lClipboardList.Count != 0)
                {
                    bDeleteClipboardEntry.IsEnabled = true;
                }               
            }
            else if(ClipboardEntry.CBInView == 0 && lClipboardList.Count != 0)
            {
                lClipboardList.RemoveAt(ClipboardEntry.CBInView);
                if(lClipboardList.Count != 0)
                {
                    tbClipboardContent.Text = lClipboardList[0].GetCBText();                    
                }
                else
                {
                    tbClipboardContent.Text = "";                   
                }
            }

            if(lClipboardList.Count == 0)
            {
                bDeleteClipboardEntry.IsEnabled = false;
                bNext.IsEnabled = false;
                bPrev.IsEnabled = false;
                ClipboardEntry.CBInView = -1;
            }

            if (lClipboardList.Count == 1)
            {
                bNext.IsEnabled = false;
                bPrev.IsEnabled = false;
            }
           
            // -- test
            SetCBInfoString();
            // -- test       
        }


        // Go to older Clipboard data
        private void bPrev_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardEntry.CBInView > 0)
            { 
                ClipboardEntry.CBInView--;
                tbClipboardContent.Text = lClipboardList[ClipboardEntry.CBInView].GetCBText();
                bNext.IsEnabled = true;                

                if (ClipboardEntry.CBInView == 0)  { bPrev.IsEnabled = false; }
            }
            bNext.IsEnabled = true;
            bDeleteClipboardEntry.IsEnabled = true;           

            // -- test
            SetCBInfoString();
            // -- test
        }


        // Go to newer Clipboard data
        private void bNext_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardEntry.CBInView < (lClipboardList.Count - 1))
            {                       
                ClipboardEntry.CBInView++;
                tbClipboardContent.Text = lClipboardList[ClipboardEntry.CBInView].GetCBText();
                
                bPrev.IsEnabled = true; 
            }
            if (ClipboardEntry.CBInView == (lClipboardList.Count - 1)) { bNext.IsEnabled = false; }
            bDeleteClipboardEntry.IsEnabled = true;
            
            // -- test
            SetCBInfoString();
            // -- test
        }


        // Unhide clipboard content 
        private void bShowClipboard_Click(object sender, RoutedEventArgs e)
        {
            bShowClipboard.Visibility = Visibility.Hidden;
        }
    

        private void bBrowseToolsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + @"Tools\");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open folder. " + Environment.NewLine + "Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.WriteLog("Failed to open folder. Error: " + ex.Message);
            }
        }


        private void bRefreshTools_Click(object sender, RoutedEventArgs e)
        {
            if (GetTools() != 0)
            {
                MessageBox.Show("Failed to get tools.");
                return;
            }

            // Empty the listBoxCommands and re-populate it with the file information
            listBoxCommands.ItemsSource = null;
            listBoxCommands.ItemsSource = FileItems;


            // Refresh RegEx XML file Re-Populate the RegExItems list with the RegExp config
            if (GetRegExConfig() != 0)
            {
                MessageBox.Show("Failed to refresh the RegExs from the XML file.");
                Logger.WriteLog("Failed to refresh the RegExs from the XML file.");
                return;
            }
        }


        // Do the RegExp and set the listbox color
        private void tbClipboardContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool bHitFlag = false;

            foreach (RegExpConf re in RegExpItems)
            {                
                if (System.Text.RegularExpressions.Regex.IsMatch(tbClipboardContent.GetLineText(0), re.RegExString))
                {
                    bHitFlag = true;

                    foreach (FileItem fitem in FileItems)
                    {
                        if (fitem.FileClass == re.RegExClass)
                        {
                            fitem.ItemBackgroundColor = "Yellow";
                            bListBoxColorSet = true;                        
                        }
                    }
                    listBoxCommands.ItemsSource = null;                    
                    listBoxCommands.ItemsSource = FileItems;
                }
            }

            if(!bHitFlag && bListBoxColorSet)
            {
                bListBoxColorSet = false;

                foreach (FileItem fitem in FileItems)
                {
                    fitem.ItemBackgroundColor = "";
                }
                listBoxCommands.ItemsSource = null;
                listBoxCommands.ItemsSource = FileItems;
            }
        }



        // Read the RegExps from the XML file and populate the "RegExpItems" list
        private int GetRegExConfig()
        {
            string sRegExXmlFile = AppDomain.CurrentDomain.BaseDirectory + @"Config\RegEx.xml";

            // Clear the RegExpItems list to not have duplicates
            RegExpItems.Clear();

            // Read the XML file
            // Source: http://stackoverflow.com/questions/5604330/xml-parsing-read-a-simple-xml-file-and-retrieve-values
            XDocument doc;
            try
            {
                doc = XDocument.Load(sRegExXmlFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"XML file """ + sRegExXmlFile + @""" contains invalid data." + Environment.NewLine + "Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.WriteLog(@"XML file """ + sRegExXmlFile + @""" contains invalid data." + Environment.NewLine + "Error: " + ex.Message);

                return 1;
            }


            foreach (XElement el in doc.Root.Elements())
            {
                string sRegExString = "";            
                string sClass = "";           
                string sRegexID = "Invalid <RegEx> element";                

                try
                {
                    sRegexID = el.Attribute("id") != null ? el.Attribute("id").Value : "Invalid <RegEx> element";
                    sRegExString = el.Element("regexstring") != null ? el.Element("regexstring").Value : "";
                    sClass = el.Element("class") != null ? el.Element("class").Value : "";

                    RegExpItems.Add(new RegExpConf() { RegExClass = sClass, RegExString = sRegExString });                     
                }
                catch
                {
                    MessageBox.Show("RegEx.XML file contains invalid data." + Environment.NewLine + "Failed at <RegEx> element: " + sRegexID, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Logger.WriteLog("RegEx.XML file contains invalid data. Failed at <RegEx> element: " + sRegexID);
                    return 1;
                }
            }
            return 0;
        }



       
        // Set the MaxWith property of the left column to the size of the window minus the size required by the components in the right column
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 456 is the size required by the groupbox plus the buttons on the right to the groupbox
            // Note: "ActualWidth is required because in full screen mode the splitter control is not working correctly when using only "Width" 
            // http://stackoverflow.com/questions/607827/what-is-the-difference-between-width-and-actualwidth-in-wpf or http://stackoverflow.com/questions/32668707/c-sharp-wpf-sizechanged-event-doesnt-update-width-and-height-when-maximized
            //xamlLeftColumn.MaxWidth = this.Width - 456;
            xamlLeftColumn.MaxWidth = this.ActualWidth - 456;            
        }


        // Start the server thread to send data over the named pipe
        // Todo: use BeginWaitForConnection instead of WaitForConnection
        static void StartServer(string sPipeName, string[] sDatatoSend)
        {            
            // TODO: use "using" for serverpipe and the other pipe vars            
            Task.Factory.StartNew(() =>
            {
                NamedPipeServerStream serverPipe = new NamedPipeServerStream(sPipeName);         
                serverPipe.WaitForConnection();
                
                StreamWriter writer = new StreamWriter(serverPipe);             

                foreach (string line in sDatatoSend)
                {
                    writer.WriteLine(line);
                }
                
                writer.Flush();
                writer.Close();                
                serverPipe.Close();                
            });

           
            // Thread that implements a 60 seconds timeout before "WaitForConnection" will be canceled. Use "BeginWaitForConnection" in future version.
            // TODO: Make the 60 seconds a setting so that it can be defined by the user
            Task.Factory.StartNew(() =>
            {
                bool isConnected = false;
                Thread.Sleep(60000);
                NamedPipeClientStream pipekill = new NamedPipeClientStream(sPipeName);
                
                try
                {
                    pipekill.Connect(5000);
                    isConnected = true;
                }
                catch (TimeoutException)
                {
                    isConnected = false;                   
                }

                if(isConnected)
                {
                    StreamReader reader = new StreamReader(pipekill);                    
                    string dump = reader.ReadToEnd(); // should be automatically GC as soon as out of IF scope
                }            
            });
           
            Logger.WriteLog("Pipe server thread started.");
        }


        // Show the Optional Arguments window
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Check if the selected file is an XML file
            if (((listBoxCommands.SelectedItem as FileItem).FileExt).ToLower() != ".xml")
            {
                MessageBox.Show("Only XML files support pre-defined optional arguments.", "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }    

            // Get the path to the selected file
            string sXmlBatFile = AppDomain.CurrentDomain.BaseDirectory + @"Tools\" + (listBoxCommands.SelectedItem as FileItem).FileName + (listBoxCommands.SelectedItem as FileItem).FileExt;

            // Init the optional arguments window
            OptionalArguments WndOptArguments = new OptionalArguments(sXmlBatFile);
            WndOptArguments.Owner = Application.Current.MainWindow;  

            // Set an event so the main window knows that it received data
            WndOptArguments.DataChanged += WndOptArguments_DataChanged;

            // Show the Optional Arguments window as a Modal Dialog Box
            WndOptArguments.ShowDialog();            
        }


        // Setup event stuff - a combination of the both articles:
        // Source: http://www.codeproject.com/Questions/1031769/Refresh-parent-window-Grid-from-child-window-in-WP <- for the event
        // Source: http://stackoverflow.com/questions/14977927/how-do-i-pass-objects-in-eventargs <- for sending data to the event receiver
        private void WndOptArguments_DataChanged(object sender, EventArgs e)
        {
            if(comboOptArg.Text == "")
            {
                comboOptArg.Text = (e as OptionalArgumentEventArgs).OptArg;
            }
            else
            {
                comboOptArg.Text = comboOptArg.Text + " " + (e as OptionalArgumentEventArgs).OptArg;
            }
        }


        private void bMoreSettings_Click(object sender, RoutedEventArgs e)
        {
            // Create the settings window
            SettingsWindow sw = new SettingsWindow();
            sw.Owner = Application.Current.MainWindow;
            sw.ShowDialog();

            // Set the external commands listbox text size
            if(Properties.Settings.Default.uiCommandsFontSize > 7 && Properties.Settings.Default.uiCommandsFontSize < 73)
            {
                // Source: http://stackoverflow.com/questions/3444371/converting-between-wpf-font-size-and-standard-font-size
                listBoxCommands.FontSize = Properties.Settings.Default.uiCommandsFontSize * 1.33333333;
            }            
        }

      
        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (this.WindowState)
            {                
                case WindowState.Minimized:
                    if(Properties.Settings.Default.bHideFromTaskbarOnMinimize)
                    {
                        // Make sure the tool can not be hidden if "cBIgnoreCBUpdate" is checked or "cBShowNW" is unchecked
                        if (cBIgnoreCBUpdate.IsChecked.Value || !cBShowNW.IsChecked.Value)
                        {
                            MessageBox.Show(@"Clipboard Accelerator can't hide if the ""Ignore clipboard update"" option is set or if the ""Show notification window..."" option is disabled.", "Note", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        this.Hide();
                    }
                    break;               
            }
        }

        private void bToClipboard_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(tbClipboardContent.Text);
        }
    }
}