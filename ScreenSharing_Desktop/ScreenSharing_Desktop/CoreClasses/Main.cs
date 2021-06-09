﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Main
{

    #region Parameters

    #endregion


    #region Variables

    public static int FPS
    {
        get
        {
            lock (Lck_FPS)
                return fps;
        }
        set
        {
            lock (Lck_FPS)
                fps = value;
        }
    }
    public static bool IsImageShowed
    {
        get
        {
            lock (Lck_IsImageShowed)
                return _isImageShowed;
        }
        set
        {
            lock (Lck_IsImageShowed)
                _isImageShowed = value;
        }
    }
    public static bool IsImageSent
    {
        get
        {
            lock (Lck_IsImageSent)
                return _isImageSent;
        }
        set
        {
            lock (Lck_IsImageSent)
                _isImageSent = value;
        }
    }
    public static Bitmap ScreenImage
    {
        get
        {
            lock (Lck_ScreenImage)
                return _screenImage;
        }
        set
        {
            lock (Lck_ScreenImage)
                _screenImage = value;
        }
    }
    public static double TransferSpeed
    {
        get
        {
            lock (Lck_TransferSpeed)
                return _transferSpeed;
        }
        set
        {
            lock (Lck_TransferSpeed)
                _transferSpeed = value;
        }
    }
    public static double Ping
    {
        get
        {
            lock (Lck_Ping)
                return _ping;
        }
        set
        {
            lock (Lck_Ping)
                _ping = value;
        }
    }
    public static  bool IsControlsEnabled
    {
        get
        {
            lock (Lck_IsControlsEnabled)
                return _isControlsEnabled;
        }
        set
        {
            lock (Lck_IsControlsEnabled)
                _isControlsEnabled = value;
        }
    }
    public static CommunicationTypes CommunicationType
    {
        get
        {
            lock (Lck_CommunitionType)
                return _communitionType;
        }
        set
        {
            lock (Lck_CommunitionType)
                _communitionType = value;
        }
    }


    private static bool _isImageShowed = true;
    private static bool _isImageSent = true;
    private static bool _isControlsEnabled = false;
    private static Bitmap _screenImage;
    private static double _transferSpeed;
    private static CommunicationTypes _communitionType;
    private static int fps = 0;
    private static double _ping = 0;
    

    private static object Lck_FPS = new object();
    private static object Lck_ScreenImage = new object();
    private static object Lck_IsImageShowed = new object();
    private static object Lck_IsImageSent = new object();
    private static object Lck_TransferSpeed = new object();
    private static object Lck_IsControlsEnabled = new object();
    private static object Lck_CommunitionType = new object();
    private static object Lck_Ping = new object();

    private static Thread SenderThread;

    #endregion

    #region MQ Variables

    private static MQPublisher Publisher;
    private static MQPublisher TimeBasePublisher;
    private static MQSubscriber Subscriber;
    private static MQSubscriber TimeBaseSubscriber;

    private static string Topic = "Screen";
    private static string TimeBaseTopic = "Time";
    public static int Port = 4112;
    public static int TimeBasePort = 4109;
    public static string MyIP;
    public static string TargetIP;
    private static Stopwatch SubStopwatch;
    private static bool IsPublisherEnabled;
    #endregion
    private static int TotalBytesReceived = 0;
    private static int FpsCounter = 0;

    private static TimeSpan PublisherTimeBase;
    private static TimeSpan SubscriberPreviousTime;

    public enum CommunicationTypes
    {
        Sender,
        Receiver
    }

    /// <summary>
    /// Initializes a MQ Publisher with defined topic at given port
    /// </summary>
    public static void StartSharing()
    {
        CommunicationType = CommunicationTypes.Sender;
        MyIP = Client.GetDeviceIP();
        Publisher = new MQPublisher(Topic, MyIP, Port);
        TimeBasePublisher = new MQPublisher(TimeBaseTopic, MyIP, TimeBasePort);
        ImageProcessing.StartScreenCapturer();
        IsPublisherEnabled = true;
        SenderThread = new Thread(PublisherCoreFcn);
        SenderThread.Start();
    }
    public static void StopSharing()
    {
        try
        {
            IsPublisherEnabled = false;
            ImageProcessing.StopScreenCapturer();
            SenderThread.Abort();
            Publisher.Stop();
            TimeBasePublisher.Stop();
            FPS = 0;
            TransferSpeed = 0;
        }
        catch
        {
            Debug.WriteLine("Failed to Stop Publisher");
        }
    }
    private static void PublisherCoreFcn()
    {
 
        Stopwatch stopwatch = Stopwatch.StartNew();
        int totalBytesSent = 0;
        while (IsPublisherEnabled)
        {
            byte[] screenBytes= ImageProcessing.GetScreenBytes();
            if(screenBytes!=null && Publisher!=null)
            {
                string time = DateTime.UtcNow.TimeOfDay.ToString();
                byte[] timeBytes = Encoding.ASCII.GetBytes(time);
                byte[] data = new byte[timeBytes.Length + screenBytes.Length];
                timeBytes.CopyTo(data, 0);
                screenBytes.CopyTo(data, timeBytes.Length);
                Publisher.Publish(data);
                totalBytesSent += data.Length;
                FpsCounter++;
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    UpdateStats(totalBytesSent, stopwatch.Elapsed.TotalSeconds);
                    stopwatch.Restart();
                    totalBytesSent = 0;
                    string timeBase = DateTime.UtcNow.TimeOfDay.ToString();
                    byte[] timeBaseBytes = Encoding.ASCII.GetBytes(timeBase);
                    TimeBasePublisher.Publish(timeBaseBytes);
                }
            }
            else
            {
                Debug.WriteLine("Capturer or Publisher was null. Transfer aborted!");
                break;
            }
        }
    }
    #region Subscriber Function

    public static void StartReceiving(string ip)
    {
        TargetIP = ip;
        Subscriber = new MQSubscriber(Topic, TargetIP, Port);
        Subscriber.OnDataReceived += Subscriber_OnDataReceived;
        TimeBaseSubscriber=new MQSubscriber(TimeBaseTopic, TargetIP, TimeBasePort);
        TimeBaseSubscriber.OnDataReceived += TimeBaseSubscriber_OnDataReceived;
        SubStopwatch = Stopwatch.StartNew();
        CommunicationType = CommunicationTypes.Receiver;
    }

    private static void TimeBaseSubscriber_OnDataReceived(byte[] data)
    {
        if (data != null)
        {
            string timeString = Encoding.ASCII.GetString(data);
            PublisherTimeBase = TimeSpan.Parse(timeString);
        }
    }

    private static void Subscriber_OnDataReceived(byte[] data)
    {
        Stopwatch stp = Stopwatch.StartNew();
        if (data != null)
        {
            byte[] timeBytes = new byte[16];
            Array.Copy(data, 0, timeBytes, 0, timeBytes.Length);
            string timeString = Encoding.ASCII.GetString(timeBytes);
            TimeSpan SentTime = TimeSpan.Parse(timeString);
            TimeSpan CurrentTime = DateTime.UtcNow.TimeOfDay;
            PublisherTimeBase += (CurrentTime - SubscriberPreviousTime);
            SubscriberPreviousTime = CurrentTime;
            double deltaTime = PublisherTimeBase.TotalMilliseconds - SentTime.TotalMilliseconds;
            if (Ping <= 0)
                Ping = deltaTime;
            Ping = Ping * 0.99 + 0.01 * deltaTime;
            byte[] ScreenBytes = new byte[data.Length - timeBytes.Length];
            Array.Copy(data, timeBytes.Length, ScreenBytes, 0, ScreenBytes.Length);
            ScreenImage = ImageProcessing.ImageFromByteArray(ScreenBytes);
            TotalBytesReceived += data.Length;
            FpsCounter++;
            if (SubStopwatch.ElapsedMilliseconds >= 1000)
            {
                UpdateStats(TotalBytesReceived, SubStopwatch.Elapsed.TotalSeconds);
                TotalBytesReceived = 0;
                SubStopwatch.Restart();
            }
            IsImageShowed = true;
        }
        else
        {
            Debug.WriteLine("image data was null!");
        }
    }
    public static void StopReceiving()
    {
        Subscriber.Stop();
        TimeBaseSubscriber.Stop();
        FPS = 0;
        TransferSpeed = 0;
    }
    #endregion

    private static void UpdateStats(int totalbytes,double time)
    {
        double mb = 1024 * 1024;
        double totalMB = totalbytes / mb;
        if (FPS != 0)
            FPS = (int)(FPS * 0.9 + 0.1 * FpsCounter);
        else
            FPS = FpsCounter;
        FpsCounter = 0;
        if (TransferSpeed != 0)
            TransferSpeed = TransferSpeed * 0.9 + 0.1 * (totalMB / time);
        else
            TransferSpeed = totalMB / time;
    }
}
