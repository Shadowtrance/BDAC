﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace BDAC
{
    public class Functions
    {
        private readonly MainFrm _mainform;

        public Functions(MainFrm frm)
        {
            _mainform = frm;
        }

        private readonly Process[] _bd64 = Process.GetProcessesByName("BlackDesert64");
        private readonly Process[] _bd32 = Process.GetProcessesByName("BlackDesert32");
        
        #region Monitoring Thread

        private ThreadStart _tsMonitor;
        private Thread _monitor;

        public bool GRunning;
        public bool GConnected;
        public bool AutoClose;

        private int _concurrentFails;
        public int MaxAttempts = 3;

        public void Monitor()
        {
            //Create a Thread to do the work
            //To keep the UI responsive/lag-less at all times
            _tsMonitor = MMonitor;
            _monitor = new Thread(_tsMonitor);

            //Start the thread
            _monitor.Start();

            //Prevent repeated work to be done
            //while the thread isn't finished
            _monitor.Join();
        }

        private void MMonitor()
        {
            //Check if game is running
            GRunning = IsProcessRunning();

            //If the game is running
            if (GRunning)
            {
                //then check if it's connected
                GConnected = IsConnected();
            }
        }

        public bool IsProcessRunning()
        {
            try
            {
                //Check if BDO's process is running
                return _bd64.Concat(_bd32).Any();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                return false;
            }
        }

        public bool IsConnected()
        {
            try
            {
                using (Process p = new Process())
                {
                    ProcessStartInfo ps = new ProcessStartInfo
                    {
                        FileName = "netstat.exe",
                        Arguments = "-n -o",
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    //netstat displays active TCP connections and ports

                    //-n Displays active TCP connections, however,
                    //addresses and port numbers are expressed
                    //numerically and no attempt is made to determine names.

                    //-o Displays active TCP connections and includes
                    //the process ID (PID) for each connection.
                    
                    p.StartInfo = ps;
                    p.Start();

                    StreamReader stdOutput = p.StandardOutput;
                    string content = stdOutput.ReadToEnd();

                    //Read netstat's output
                    string[] rows = Regex.Split(content, "\r\n");
                    if (
                        rows.Select(t => Regex.Split(t, "\\s+"))
                            .Where(
                                tokens =>
                                    tokens.Length > 4 && (tokens[1].Equals("UDP") || tokens[1].Equals("TCP")) &&
                                    tokens[4].Equals("ESTABLISHED"))
                            .Any(
                                tokens =>
                                    Process.GetProcessById(Convert.ToInt32(tokens[5]))
                                        .ProcessName.Contains("BlackDesert")))
                    {
                        _concurrentFails = 0;
                        return true;
                    }

                    if (_mainform.nCloseDC.Checked)
                    {
                        _concurrentFails++;
                        Log("Failed to detect a connection " + _concurrentFails + " time(s). Will attempt " + (MaxAttempts - _concurrentFails) + " more times.");
                    }

                    //BDO has no active connection
                    if (_mainform.nCloseDC.Checked && _concurrentFails >= MaxAttempts)
                    {
                        AutoClose = true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                _mainform.traySystem.Text = @"BDAC - Disconnected";
                return false;
            }
        }

        public void CloseGame()
        {
            try
            {
                if (_mainform.nCloseDC.Checked)
                {
                    foreach (Process bd in _bd64.Concat(_bd32))
                    {
                        if (!bd.HasExited)
                        {
                            Log(bd.ProcessName);
                            bd.Kill();
                            bd.WaitForExit();
                            _mainform.runLbl.Text = @"Auto Closed";
                            _mainform.runLbl.ForeColor = Color.Red;
                            _mainform.startCheckBtn.Text = @"BDO Auto Closed";
                            _mainform.traySystem.Text = @"BDAC - Auto Closed";
                            _mainform.runLed.On = true;
                            _mainform.runLed.Color = Color.Red;

                            if (_mainform.nShutdownDC.Checked)
                            {
                                _mainform.checkShutdown.Start();
                            }
                        }
                        else
                        {
                            Log("BDO not running.");
                        }
                        break;
                    }
                    _mainform.checkGameTimer.Stop();
                    Log("Killed all running instances of bdo.");
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }

        public void Log(string msg)
        {
            StreamWriter writer = new StreamWriter(_mainform.Logfile, true);
            writer.WriteLine(DateTime.Now.ToString("[MMM dd yyyy] HH:mm:ss | ") + msg);
            writer.Close();
        }

        public void ShutdownPc()
        {
            //Start the shutdown
            Process.Start("shutdown", "/s /t 0");

            // the argument /s is to shut down the computer.
            // the argument /t sets the time-out period before doing (how long until shutdown)
            // the operation, in our case we have it set to no time-out. 
        }

        #endregion
    }
}
