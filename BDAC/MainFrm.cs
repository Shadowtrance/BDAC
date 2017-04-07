﻿using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace BDAC
{
    public partial class MainFrm : Form
    {
        public Functions Functions;
        public int CloseTime;
        public int ShutdownPc;
        public int MinTime;
        public string Logfile = "bdaclog.txt";

        private readonly AssemblyName _assemblyName = Assembly.GetExecutingAssembly().GetName();

        public readonly AppConfig ConfigManager = new AppConfig();

        #region MainFrm

        public MainFrm()
        {
            Functions = new Functions(this);
            InitializeComponent();

            runLed.On = false;
            dcLed.On = false;
        }

        private void MainFrm_Load(object sender, EventArgs e)
        {
            if (!File.Exists(AppConfig.ConfigFile))
            {
                Functions.Log("Settings file not found, creating a new one.");
                ConfigManager.SaveConfig();

                iTalk_TabControl1.SelectedTab = settingsTabPage;
            }
            else
            {
                Functions.Log("Settings file found, loading settings.");
                ConfigManager.LoadConfig();

                nMinBox.Checked = ConfigManager.Config.Tray;
                nCloseDC.Checked = ConfigManager.Config.AutoClose;
                nShutdownDC.Checked = ConfigManager.Config.ShutDown;

                Functions.Log("Settings Loaded.");
                iTalk_TabControl1.SelectedTab = mainTabPage;
            }
        }

        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigManager.Config.Tray = nMinBox.Checked;
            ConfigManager.Config.AutoClose = nCloseDC.Checked;
            ConfigManager.Config.ShutDown = nShutdownDC.Checked;
            ConfigManager.SaveConfig();
            Functions.Log("Settings Saved.");
        }

        private void MainFrm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                traySystem.Visible = true;
            }
        }

        private void traySystem_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //Reveal BDAC when double clicking the tray system
            Show();
            WindowState = FormWindowState.Normal;
            traySystem.Visible = false;
            themeContainer.Text = _assemblyName.Name;
        }

        #endregion

        #region Main Tab

        private void startCheckBtn_Click(object sender, EventArgs e)
        {
            if (!Functions.AutoClose)
            {
                if (!checkGameTimer.Enabled)
                {
                    startCheckBtn.Text = @"Stop Monitoring";
                    checkGame_Tick(this, null);
                    checkGameTimer.Start();

                    //Auto Minimize bdac after 5 seconds if setting is checked and game is running and connected.
                    if (nMinBox.Checked && runLbl.Text == @"Running" && dcLbl.Text == @"Connected")
                    {
                        MinTime = 0;
                        minTimer.Start();
                    }
                    Functions.Log("Started monitoring.");
                }
                else
                {
                    runLed.On = false;
                    runLbl.ForeColor = Color.Red;
                    runLbl.Text = @"N/A";

                    dcLed.On = false;
                    dcLbl.ForeColor = Color.Red;
                    dcLbl.Text = @"N/A";

                    traySystem.Text = @"BDAC";

                    startCheckBtn.Text = @"Start Monitoring";
                    checkGameTimer.Stop();

                    themeContainer.Text = _assemblyName.Name;
                    minTimer.Stop();
                    Functions.Log("Stopped monitoring.");
                }
            }
            else
            {
                checkAutoClose.Stop();
                checkShutdown.Stop();

                Functions.AutoClose = false;

                startCheckBtn.Text = @"Stop Monitoring";
                checkGameTimer.Start();
                Functions.Log("Started monitoring.");
            }
        }

        private void checkGame_Tick(object sender, EventArgs e)
        {
            //Stop doing extra work if the game is scheduled for closing
            if (Functions.AutoClose)
            {
                checkGameTimer.Stop();

                CloseTime = 0;
                checkAutoClose.Start();
                Functions.Log(@"Auto closing BDO in 60 seconds.");
                return;
            }

            //Do checks on a new thread
            Functions.Monitor();

            //Update the labels depending on the results of the checks
            switch (Functions.GRunning)
            {
                case true:
                    runLbl.ForeColor = Color.Green;
                    runLbl.Text = @"Running";
                    runLed.On = true;
                    runLed.Color = Color.Green;
                    Functions.Log("BDO is running...");
                    break;
                case false:
                    runLbl.ForeColor = Color.Red;
                    runLbl.Text = @"Not Running";
                    runLed.On = true;
                    runLed.Color = Color.Red;
                    Functions.Log("BDO is not running...");
                    break;
            }

            switch (Functions.GConnected)
            {
                case true:
                    dcLbl.ForeColor = Color.Green;
                    dcLbl.Text = @"Connected";
                    traySystem.Text = @"BDAC - Connected";
                    dcLed.On = true;
                    dcLed.Color = Color.Green;
                    Functions.Log("BDO is connected...");
                    break;
                case false:
                    dcLbl.ForeColor = Color.Red;
                    dcLbl.Text = @"Disconnected";
                    traySystem.Text = @"BDAC - Disconnected";
                    dcLed.On = true;
                    dcLed.Color = Color.Red;
                    Functions.Log("BDO is not connected...");
                    break;
            }
        }

        private void checkAutoClose_Tick(object sender, EventArgs e)
        {
            //Close the game after 60 seconds
            if (CloseTime >= 60)
            {
                startCheckBtn.Enabled = false;
                startCheckBtn.Text = @"Auto Closing BDO";

                checkAutoClose.Stop();
                Functions.CloseGame();

                //Schedule shutdown if
                //the option is checked
                if (nShutdownDC.Checked)
                {
                    checkShutdown.Start();
                    Functions.Log(@"Shutting PC down in 5 minutes.");
                    startCheckBtn.Enabled = true;
                    return;
                }

                startCheckBtn.Enabled = true;
                return;
            }

            startCheckBtn.Text = @"Auto closing BDO in " + (60 - CloseTime) + @" second(s) [Cancel]";
            CloseTime++;
        }

        private void checkShutdown_Tick(object sender, EventArgs e)
        {
            //Shutdown PC after 5 minutes
            if (ShutdownPc >= 300)
            {
                startCheckBtn.Enabled = false;
                startCheckBtn.Text = @"Shutting PC down";

                checkShutdown.Stop();
                Functions.ShutdownPc();

                return;
            }

            startCheckBtn.Text = @"Shutting PC down in " + TimeSpan.FromSeconds(300 - ShutdownPc).ToString(@"mm\:ss") + @" min(s) [Cancel]";
            ShutdownPc++;
        }

        private void minTimer_Tick(object sender, EventArgs e)
        {
            //Auto Minimize bdac after 5 seconds
            if (MinTime >= 5)
            {
                Hide();
                minTimer.Stop();
                themeContainer.Text = _assemblyName.Name;
            }
            themeContainer.Text = @"Minimizing in " + (5 - MinTime) + @" seconds";
            MinTime++;
        }

        #endregion

        #region Settings Tab

        private void nMinBox_CheckedChanged(object sender)
        {
            ConfigManager.Config.Tray = nMinBox.Checked;
            ConfigManager.SaveConfig();
        }

        private void nCloseDC_CheckedChanged(object sender)
        {
            ConfigManager.Config.AutoClose = nCloseDC.Checked;
            ConfigManager.SaveConfig();
        }

        private void nShutdownDC_CheckedChanged(object sender)
        {
            ConfigManager.Config.ShutDown = nShutdownDC.Checked;
            ConfigManager.SaveConfig();
        }

        #endregion
    }
}
