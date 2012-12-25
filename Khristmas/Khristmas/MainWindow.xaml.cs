using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Kinect;
using System.Threading;
using System.Windows.Threading;

namespace Khristmas
{
    public partial class MainWindow : Window
    {
        private KinectSensor _Kinect;
        private WriteableBitmap _ColorImageBitmap;
        private Int32Rect _ColorImageBitmapRect;
        private int _ColorImageStride;
        private Skeleton[] FrameSkeletons;
        Joint primaryHand;

        static Button selected;
        List<Button> buttons;

        public MainWindow()
        {
            InitializeComponent();

            buttons = new List<Button> { goButton };
            handImage.Click += new RoutedEventHandler(handImage_Click);

            this.Loaded += (s, e) => { DiscoverKinectSensor(); };
            this.Unloaded += (s, e) => { this.Kinect = null; };
        }

        private void DiscoverKinectSensor()
        {
            KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;
            this.Kinect = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
        }

        private void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Connected:
                    if (this.Kinect == null)
                    {
                        this.Kinect = e.Sensor;
                    }
                    break;
                case KinectStatus.Disconnected:
                    if (this.Kinect == e.Sensor)
                    {
                        this.Kinect = null;
                        this.Kinect = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
                        if (this.Kinect == null)
                        {
                            MessageBox.Show("Sensor Disconnected. Please reconnect to continue.");
                        }
                    }
                    break;
            }
        }

        public KinectSensor Kinect
        {
            get { return this._Kinect; }
            set
            {
                if (this._Kinect != value)
                {
                    if (this._Kinect != null)
                    {
                        UninitializeKinectSensor(this._Kinect);
                        this._Kinect = null;
                    }
                    if (value != null && value.Status == KinectStatus.Connected)
                    {
                        this._Kinect = value;
                        InitializeKinectSensor(this._Kinect);
                    }
                }
            }
        }

        private void UninitializeKinectSensor(KinectSensor kinectSensor)
        {
            if (kinectSensor != null)
            {
                kinectSensor.Stop();
                kinectSensor.ColorFrameReady -= Kinect_ColorFrameReady;
                kinectSensor.SkeletonFrameReady -= Kinect_SkeletonFrameReady;
            }
        }

        private void InitializeKinectSensor(KinectSensor kinectSensor)
        {
            if (kinectSensor != null)
            {
                ColorImageStream colorStream = kinectSensor.ColorStream;
                colorStream.Enable();
                this._ColorImageBitmap = new WriteableBitmap(colorStream.FrameWidth, colorStream.FrameHeight,
                    96, 96, PixelFormats.Bgr32, null);
                this._ColorImageBitmapRect = new Int32Rect(0, 0, colorStream.FrameWidth, colorStream.FrameHeight);
                this._ColorImageStride = colorStream.FrameWidth * colorStream.FrameBytesPerPixel;
                videoStream.Source = this._ColorImageBitmap;

                kinectSensor.SkeletonStream.Enable(new TransformSmoothParameters()
                {
                    Correction = 0.5f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f,
                    Smoothing = 0.5f
                });

                kinectSensor.SkeletonFrameReady += Kinect_SkeletonFrameReady;
                kinectSensor.ColorFrameReady += Kinect_ColorFrameReady;
                kinectSensor.Start();
                this.FrameSkeletons = new Skeleton[this.Kinect.SkeletonStream.FrameSkeletonArrayLength];

            }
        }

        private void Kinect_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame frame = e.OpenColorImageFrame())
            {
                if (frame != null)
                {
                    byte[] pixelData = new byte[frame.PixelDataLength];
                    frame.CopyPixelDataTo(pixelData);
                    this._ColorImageBitmap.WritePixels(this._ColorImageBitmapRect, pixelData,
                        this._ColorImageStride, 0);
                }
            }
        }

        private void Kinect_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    frame.CopySkeletonDataTo(this.FrameSkeletons);
                    Skeleton skeleton = GetPrimarySkeleton(this.FrameSkeletons);

                    if (skeleton == null)
                    {
                        handImage.Visibility = System.Windows.Visibility.Collapsed;
                        gift1Image.Visibility = System.Windows.Visibility.Collapsed;
                    }
                    else
                    {
                        primaryHand = skeleton.Joints[JointType.HandRight];
                        TrackHand(primaryHand);
                    }
                }
            }
        }

        private void TrackHand(Joint hand)
        {
            if (hand.TrackingState == JointTrackingState.NotTracked)
            {
                handImage.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                float x;
                float y;
                if (!isPlaying) handImage.Visibility = System.Windows.Visibility.Visible;
                DepthImagePoint point = this.Kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(hand.Position, DepthImageFormat.Resolution640x480Fps30);
                x = (int)((point.X * mainCanvas.ActualWidth / this.Kinect.DepthStream.FrameWidth) -
                    (handImage.ActualWidth / 2.0));
                y = (int)((point.Y * mainCanvas.ActualHeight / this.Kinect.DepthStream.FrameHeight) -
                    (handImage.ActualHeight / 2.0));
                Canvas.SetLeft(handImage, x);
                Canvas.SetTop(handImage, y);

                if (isHandOver(handImage, buttons)) handImage.Hovering();
                else handImage.Release();
                if (hand.JointType == JointType.HandRight)
                {
                    handImage.ImageSource = "/Images/RightHand.png";
                    handImage.ActiveImageSource = "/Images/RightHand.png";
                }
                else
                {
                    handImage.ImageSource = "/Images/RightHand.png";
                    handImage.ActiveImageSource = "/Images/RightHand.png";
                }
            }
        }

        private bool isHandOver(FrameworkElement hand, List<Button> buttonslist)
        {
            var handTopLeft = new Point(Canvas.GetLeft(hand), Canvas.GetTop(hand));
            var handX = handTopLeft.X + hand.Width / 2;
            var handY = handTopLeft.Y + hand.Height / 2;

            foreach (Button target in buttonslist)
            {
                Point targetTopLeft = target.PointToScreen(new Point());
                BrushConverter bc = new BrushConverter();
                if (handX > targetTopLeft.X - target.Width/2 &&
                    handX < targetTopLeft.X + target.Width/2 &&
                    handY > targetTopLeft.Y - target.Height/2 &&
                    handY < targetTopLeft.Y + target.Height/2)
                {
                    switch (target.Name)
                    {
                        case "goButton":
                            {
                                target.Background = (Brush)bc.ConvertFrom("#FFE4DC00");
                                break;
                            }
                    }
                    selected = target;
                    return true;
                }
                else
                {
                    switch (target.Name)
                    {
                        case "goButton":
                            {
                                target.Background = (Brush)bc.ConvertFrom("#FFFFF600");
                                break;
                            }
                    }
                }
            }
            return false;
        }

        private static Skeleton GetPrimarySkeleton(Skeleton[] skeletons)
        {
            Skeleton skeleton = null;
            if (skeletons != null)
            {
                for (int i = 0; i < skeletons.Length; i++)
                {
                    if (skeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        if (skeleton == null)
                        {
                            skeleton = skeletons[i];
                        }
                        else
                        {
                            if (skeleton.Position.Z > skeletons[i].Position.Z)
                            {
                                skeleton = skeletons[i];
                            }
                        }
                    }
                }
            }
            return skeleton;
        }

        void handImage_Click(object sender, RoutedEventArgs e)
        {
            selected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, selected));
        }

        private void goButton_Click(object sender, RoutedEventArgs e)
        {
            startText.Visibility = System.Windows.Visibility.Collapsed;
            goButton.Visibility = System.Windows.Visibility.Collapsed;
            handImage.Visibility = System.Windows.Visibility.Hidden;
            buttons.Remove(goButton);

            initGame();
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 30);
            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            updateGame();
            time -= 30;
            timeDisplay.Text = "Time left: " + ((int)time / 1000);
            if (time < 0)
            {
                isPlaying = false;
                dispatcherTimer.Stop();
                startText.Text = "Game Over!\nYour score is " + score;
                goButton.Content = "Play Again";
                buttons.Add(goButton);
                startText.Visibility = System.Windows.Visibility.Visible;
                goButton.Visibility = System.Windows.Visibility.Visible;
            }
        }

        void initGame()
        {
            gift1Image.Visibility = System.Windows.Visibility.Visible;
            gift2Image.Visibility = System.Windows.Visibility.Visible;

            gift1ImageX = rand.Next(0, (int)mainCanvas.Width - (int)gift1Image.Width);
            Canvas.SetLeft(gift1Image, gift1ImageX);
            gift2ImageX = rand.Next(0, (int)mainCanvas.Width - (int)gift2Image.Width);
            Canvas.SetLeft(gift2Image, gift2ImageX);


            timeDisplay.Text = "Time left: 60";
            scoreDisplay.Text = "Score: 0";
            time = 60000;
            score = 0;
            gift1Speed = 1;
            gift2Speed = 1;
            gift1ImageY = -gift1Image.Height;
            gift2ImageY = -gift2Image.Height;
            isPlaying = true;
        }

        DispatcherTimer dispatcherTimer;
        int time = 60000;
        bool isPlaying = false;
        double gift1ImageX = 0;
        double gift1ImageY = 0;
        double gift1Speed = 1;

        double gift2ImageX = 0;
        double gift2ImageY = 0;
        double gift2Speed = 1;

        Random rand = new Random(1);
        int score = 0;
        float HitRadius = 60;

        private void updateGame()
        {
            var handTopLeft = new Point(Canvas.GetLeft(handImage), Canvas.GetTop(handImage));
            var handX = handTopLeft.X + handImage.ActualWidth / 2;
            var handY = handTopLeft.Y + handImage.ActualHeight / 2;

            Canvas.SetTop(gift1Image, gift1ImageY);
            gift1ImageY = gift1ImageY + gift1Speed;
            double gift1ImageCenterX = gift1ImageX + (gift1Image.Width / 2);
            double gift1ImageCenterY = gift1ImageY + (gift1Image.Height / 2);

            Canvas.SetTop(gift2Image, gift2ImageY);
            gift2ImageY = gift2ImageY + gift2Speed;
            double gift2ImageCenterX = gift2ImageX + (gift2Image.Width / 2);
            double gift2ImageCenterY = gift2ImageY + (gift2Image.Height / 2);

            System.Windows.Vector hitVector1 =
                new System.Windows.Vector(handX - gift1ImageX, handY - gift1ImageY);
            if (hitVector1.Length < HitRadius)
            {
                score++;
                scoreDisplay.Text = "Score: " + score.ToString();

                gift1ImageX = rand.Next(0, (int)mainCanvas.Width - (int)gift1Image.Width);
                Canvas.SetLeft(gift1Image, gift1ImageX);
                gift1ImageY = -gift1Image.Height;
            }

            System.Windows.Vector hitVector2 =
                new System.Windows.Vector(handX - gift2ImageX, handY - gift2ImageY);
            if (hitVector2.Length < HitRadius)
            {
                score++;
                scoreDisplay.Text = "Score: " + score.ToString();

                gift2ImageX = rand.Next(0, (int)mainCanvas.Width - (int)gift2Image.Width);
                Canvas.SetLeft(gift2Image, gift2ImageX);
                gift2ImageY = -gift2Image.Height;
            }

            if (gift1ImageY > mainCanvas.Height)
            {
                gift1ImageX = rand.Next(0, (int)mainCanvas.Width - (int)gift1Image.Width);
                Canvas.SetLeft(gift1Image, gift1ImageX);
                gift1ImageY = -gift1Image.Height;
            }

            if (gift2ImageY > mainCanvas.Height)
            {
                gift2ImageX = rand.Next(0, (int)mainCanvas.Width - (int)gift2Image.Width);
                Canvas.SetLeft(gift2Image, gift2ImageX);
                gift2ImageY = -gift2Image.Height;
            }

            //increase difficulty
            if (score > 2) gift1Speed = 2;
            if (score > 4) gift1Speed = 4;
            if (score > 8) gift1Speed = 6;
            if (score > 10) gift2Speed = 5;
        }
    }
}
