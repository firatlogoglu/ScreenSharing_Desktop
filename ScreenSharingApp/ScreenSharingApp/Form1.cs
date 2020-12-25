﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EkranPaylaşımUygulaması
{
    public partial class Form1 : Form
    {
        private Main main;
        private Thread uiUpdateThread;
        private int UI_UpdateFrequency = 40;        /// Hz
        private double UI_UpdatePeriod;
        private bool ui_updateEnabled = false;
        private int FormWidth;
        private int FormHeigth;
        public Form1()
        {
            InitializeComponent();
            UI_UpdatePeriod = 1.0 / UI_UpdateFrequency;
            Control.CheckForIllegalCrossThreadCalls = false;
            FormWidth = this.Width;
            FormHeigth = this.Height;
        }
        private void toolStrip_Share_Click(object sender, EventArgs e)
        {
            main = new Main(Main.CommunicationTypes.Sender);
            main.StartSharingScreen();
            StartUiThread();
        }

        private void toolStrip_Connect_Click(object sender, EventArgs e)
        {
            string ip = txt_IP.Text;
            main = new Main(Main.CommunicationTypes.Receiver);
            main.StartReceiving(ip);
            StartUiThread();
        }

        private void tool_EnableControls_Click(object sender, EventArgs e)
        {
            if (main == null)
                return;
            if (main.IsControlsEnabled)
            {
                main.IsControlsEnabled = false;
                tool_EnableControls.BackColor = Color.LightGray;
            }
            else
            {
                main.IsControlsEnabled = true;
                tool_EnableControls.BackColor = Color.LightBlue;

            }
        }
        private void UpdateUI()
        {
            Stopwatch stp = Stopwatch.StartNew();
            while(ui_updateEnabled)
            {
                if (main.IsImageReceived || main.IsImageSent)
                {
                    picture_screen.Image = main.ScreenImage;
                    lbl_FPS.Text = main.FPS.ToString();
                    lbl_TransferSpeed.Text = main.TransferSpeed.ToString("0.00") + " MB/s";
                    main.IsImageReceived = false;
                    main.IsImageSent = false;
                }
                if(FormHeigth != this.Height || FormWidth!=this.Width)
                {
                    picture_screen.Location = new Point(0, picture_screen.Location.Y);
                    picture_screen.Width = this.Width;
                    picture_screen.Height = this.Height-picture_screen.Location.Y;
                    FormWidth = this.Width;
                    FormHeigth = this.Height;

                }
                while (stp.Elapsed.TotalSeconds <= UI_UpdatePeriod);
                stp.Restart();
            }
        }
        private void StartUiThread()
        {
            ui_updateEnabled = true;
            uiUpdateThread = new Thread(UpdateUI);
            uiUpdateThread.Start();
        }
        private void StopUiThread()
        {
            ui_updateEnabled = false;
            if(uiUpdateThread!=null)
            {
                if(uiUpdateThread.IsAlive)
                {
                    try
                    {
                        uiUpdateThread.Abort();
                        uiUpdateThread = null;
                    }
                    catch
                    {

                    }
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopUiThread();
            if (main!=null)
            {
                main.CancelSharing();
                main.StopReceiving();
            }
            Application.Exit();
        }


        
    }
}
