using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace CameraApp
{
    public class MainForm : Form
    {
        private VideoCaptureDevice? videoSource;
        private FilterInfoCollection? videoDevices;
        private PictureBox? pictureBox;
        private Button? btnConnect;
        private Button? btnCapture;
        private Button? btnRecord;
        private Button? btnSelectDirectory;
        private NumericUpDown? numCaptureDelay;
        private NumericUpDown? numRecordDuration;
        private Label? lblStatus;
        private Label? lblOutputDir;
        private Label? lblCurrentTime;
        private Label? lblCountdown;
        private ComboBox? cmbCameras;
        private AppSettings? settings;
        private bool isRecording = false;
        private bool isCapturing = false;
        private string? outputDirectory;
        private string? currentRecordPath;
        private DateTime recordStartTime;
        private System.Windows.Forms.Timer? timerClock;
        private System.Windows.Forms.Timer? timerCountdown;
        private double remainingSeconds = 0;

        public MainForm()
        {
            // 載入設定
            settings = AppSettings.Load();
            outputDirectory = settings.OutputDirectory;
            
            InitializeComponent();
            
            // 從設定檔載入數值到 UI
            LoadSettingsToUI();
        }
        
        private void LoadSettingsToUI()
        {
            if (settings != null)
            {
                if (numCaptureDelay != null)
                {
                    numCaptureDelay.Value = settings.CaptureDelay;
                }
                if (numRecordDuration != null)
                {
                    numRecordDuration.Value = settings.RecordDuration;
                }
            }
        }

        private void InitializeComponent()
        {
            InitializeUI();
            InitializeTimers();
            CheckForCameras();
        }

        private void InitializeTimers()
        {
            // 時鐘計時器
            timerClock = new System.Windows.Forms.Timer
            {
                Interval = 1000 // 每秒更新一次
            };
            timerClock.Tick += TimerClock_Tick;
            timerClock.Start();

            // 倒數計時器
            timerCountdown = new System.Windows.Forms.Timer
            {
                Interval = 100 // 每100毫秒更新一次
            };
            timerCountdown.Tick += TimerCountdown_Tick;
        }

        private void TimerClock_Tick(object? sender, EventArgs e)
        {
            if (lblCurrentTime != null)
            {
                lblCurrentTime.Text = $"當前時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private void TimerCountdown_Tick(object? sender, EventArgs e)
        {
            if (remainingSeconds > 0)
            {
                remainingSeconds -= 0.1;
                if (remainingSeconds < 0) remainingSeconds = 0;

                if (lblCountdown != null)
                {
                    if (isCapturing)
                    {
                        lblCountdown.Text = $"拍照倒數：{remainingSeconds:F1} 秒";
                    }
                    else if (isRecording)
                    {
                        lblCountdown.Text = $"錄影剩餘：{remainingSeconds:F1} 秒";
                    }
                }
            }
            else
            {
                timerCountdown?.Stop();
                if (lblCountdown != null && !isRecording && !isCapturing)
                {
                    lblCountdown.Text = "";
                }
            }
        }

        private void InitializeUI()
        {
            this.Text = "相機應用程式";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 相機選擇下拉選單
            var lblCamera = new Label
            {
                Text = "選擇相機：",
                Location = new Point(10, 10),
                Size = new Size(80, 23)
            };
            this.Controls.Add(lblCamera);

            cmbCameras = new ComboBox
            {
                Location = new Point(100, 10),
                Size = new Size(300, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(cmbCameras);

            // 連接按鈕
            btnConnect = new Button
            {
                Text = "連接相機",
                Location = new Point(410, 10),
                Size = new Size(100, 30)
            };
            btnConnect.Click += BtnConnect_Click;
            this.Controls.Add(btnConnect);

            // 預覽畫面
            pictureBox = new PictureBox
            {
                Location = new Point(10, 50),
                Size = new Size(640, 480),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            this.Controls.Add(pictureBox);

            // 拍照延遲設定
            var lblCaptureDelay = new Label
            {
                Text = "拍照延遲（秒）：",
                Location = new Point(670, 50),
                Size = new Size(120, 23)
            };
            this.Controls.Add(lblCaptureDelay);

            numCaptureDelay = new NumericUpDown
            {
                Location = new Point(670, 75),
                Size = new Size(120, 23),
                Minimum = 0,
                Maximum = 60,
                Value = 0,
                DecimalPlaces = 1,
                Increment = 0.5m
            };
            numCaptureDelay.ValueChanged += NumCaptureDelay_ValueChanged;
            this.Controls.Add(numCaptureDelay);

            // 錄影時長設定
            var lblRecordDuration = new Label
            {
                Text = "錄影時長（秒）：",
                Location = new Point(670, 110),
                Size = new Size(120, 23)
            };
            this.Controls.Add(lblRecordDuration);

            numRecordDuration = new NumericUpDown
            {
                Location = new Point(670, 135),
                Size = new Size(120, 23),
                Minimum = 1,
                Maximum = 300,
                Value = 10,
                DecimalPlaces = 1,
                Increment = 1
            };
            numRecordDuration.ValueChanged += NumRecordDuration_ValueChanged;
            this.Controls.Add(numRecordDuration);

            // 拍照按鈕
            btnCapture = new Button
            {
                Text = "拍照",
                Location = new Point(670, 180),
                Size = new Size(120, 40),
                Enabled = false
            };
            btnCapture.Click += BtnCapture_Click;
            this.Controls.Add(btnCapture);

            // 錄影按鈕
            btnRecord = new Button
            {
                Text = "開始錄影",
                Location = new Point(670, 230),
                Size = new Size(120, 40),
                Enabled = false
            };
            btnRecord.Click += BtnRecord_Click;
            this.Controls.Add(btnRecord);

            // 狀態標籤
            lblStatus = new Label
            {
                Text = "狀態：未連接",
                Location = new Point(10, 490),
                Size = new Size(800, 23)
            };
            this.Controls.Add(lblStatus);

            // 選擇目錄按鈕
            btnSelectDirectory = new Button
            {
                Text = "選擇目錄",
                Location = new Point(670, 280),
                Size = new Size(120, 30)
            };
            btnSelectDirectory.Click += BtnSelectDirectory_Click;
            this.Controls.Add(btnSelectDirectory);

            // 輸出目錄標籤
            lblOutputDir = new Label
            {
                Text = $"輸出目錄：{outputDirectory}",
                Location = new Point(10, 515),
                Size = new Size(800, 23)
            };
            this.Controls.Add(lblOutputDir);

            // 當前時間標籤
            lblCurrentTime = new Label
            {
                Text = $"當前時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Location = new Point(10, 540),
                Size = new Size(300, 23),
                Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold),
                ForeColor = Color.Blue
            };
            this.Controls.Add(lblCurrentTime);

            // 倒數計時標籤
            lblCountdown = new Label
            {
                Text = "",
                Location = new Point(320, 540),
                Size = new Size(300, 23),
                Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold),
                ForeColor = Color.Red
            };
            this.Controls.Add(lblCountdown);
        }

        private void BtnSelectDirectory_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇輸出目錄";
                dialog.SelectedPath = outputDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    outputDirectory = dialog.SelectedPath;
                    if (settings != null)
                    {
                        settings.OutputDirectory = outputDirectory;
                        settings.Save();
                    }
                    
                    if (lblOutputDir != null)
                    {
                        lblOutputDir.Text = $"輸出目錄：{outputDirectory}";
                    }
                    
                    UpdateStatus($"輸出目錄已更改為：{outputDirectory}");
                }
            }
        }

        private void NumCaptureDelay_ValueChanged(object? sender, EventArgs e)
        {
            if (settings != null && numCaptureDelay != null)
            {
                settings.CaptureDelay = numCaptureDelay.Value;
                settings.Save();
            }
        }

        private void NumRecordDuration_ValueChanged(object? sender, EventArgs e)
        {
            if (settings != null && numRecordDuration != null)
            {
                settings.RecordDuration = numRecordDuration.Value;
                settings.Save();
            }
        }

        private void CheckForCameras()
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                
                if (videoDevices.Count == 0)
                {
                    UpdateStatus("未偵測到相機");
                    MessageBox.Show("未偵測到相機設備！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                cmbCameras!.Items.Clear();
                foreach (FilterInfo device in videoDevices)
                {
                    cmbCameras.Items.Add(device.Name);
                }
                cmbCameras.SelectedIndex = 0;
                UpdateStatus($"偵測到 {videoDevices.Count} 個相機設備");
            }
            catch (Exception ex)
            {
                UpdateStatus($"檢查相機時發生錯誤：{ex.Message}");
                MessageBox.Show($"檢查相機時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnConnect_Click(object? sender, EventArgs e)
        {
            try
            {
                if (videoSource != null && videoSource.IsRunning)
                {
                    // 斷開連接
                    videoSource.SignalToStop();
                    videoSource.WaitForStop();
                    videoSource = null;
                    btnConnect!.Text = "連接相機";
                    btnCapture!.Enabled = false;
                    btnRecord!.Enabled = false;
                    pictureBox!.Image = null;
                    UpdateStatus("已斷開相機連接");
                }
                else
                {
                    // 連接相機
                    if (videoDevices == null || videoDevices.Count == 0)
                    {
                        MessageBox.Show("沒有可用的相機設備！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (cmbCameras!.SelectedIndex < 0)
                    {
                        MessageBox.Show("請選擇一個相機！", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    videoSource = new VideoCaptureDevice(videoDevices[cmbCameras.SelectedIndex].MonikerString);
                    videoSource.NewFrame += VideoSource_NewFrame;
                    videoSource.Start();
                    btnConnect!.Text = "斷開連接";
                    btnCapture!.Enabled = true;
                    btnRecord!.Enabled = true;
                    UpdateStatus("相機已連接");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"連接相機時發生錯誤：{ex.Message}");
                MessageBox.Show($"連接相機時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                if (pictureBox != null && pictureBox.InvokeRequired)
                {
                    pictureBox.Invoke(new Action(() =>
                    {
                        var oldImage = pictureBox.Image;
                        pictureBox.Image = (Bitmap)eventArgs.Frame.Clone();
                        oldImage?.Dispose();
                    }));
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"顯示畫面時發生錯誤：{ex.Message}");
            }
        }

        private async void BtnCapture_Click(object? sender, EventArgs e)
        {
            if (videoSource == null || !videoSource.IsRunning)
            {
                MessageBox.Show("請先連接相機！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (isCapturing) return;

            btnCapture!.Enabled = false;
            isCapturing = true;
            double delaySeconds = (double)numCaptureDelay!.Value;
            
            if (delaySeconds > 0)
            {
                UpdateStatus($"將在 {delaySeconds} 秒後拍照...");
                remainingSeconds = delaySeconds;
                timerCountdown?.Start();

                // 倒數計時
                while (remainingSeconds > 0 && isCapturing)
                {
                    await Task.Delay(100);
                }
                
                timerCountdown?.Stop();
            }

            if (!isCapturing) // 如果被取消
            {
                btnCapture.Enabled = true;
                return;
            }

            try
            {
                if (pictureBox?.Image != null)
                {
                    string directory = GetTimestampedDirectory();
                    string fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string filePath = Path.Combine(directory, fileName);

                    pictureBox.Image.Save(filePath, ImageFormat.Jpeg);
                    UpdateStatus($"照片已儲存：{filePath}");
                    MessageBox.Show($"照片已儲存至：\n{filePath}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    UpdateStatus("無法拍照：沒有畫面");
                    MessageBox.Show("無法拍照：沒有畫面", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"拍照時發生錯誤：{ex.Message}");
                MessageBox.Show($"拍照時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isCapturing = false;
                btnCapture.Enabled = true;
                if (lblCountdown != null)
                {
                    lblCountdown.Text = "";
                }
            }
        }

        private async void BtnRecord_Click(object? sender, EventArgs e)
        {
            if (videoSource == null || !videoSource.IsRunning)
            {
                MessageBox.Show("請先連接相機！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!isRecording)
            {
                // 開始錄影
                isRecording = true;
                btnRecord!.Text = "停止錄影";
                btnCapture!.Enabled = false;
                numRecordDuration!.Enabled = false;
                recordStartTime = DateTime.Now;

                string directory = GetTimestampedDirectory();
                string fileName = $"video_{DateTime.Now:yyyyMMdd_HHmmss}.avi";
                currentRecordPath = Path.Combine(directory, fileName);

                UpdateStatus($"開始錄影，將錄製 {numRecordDuration.Value} 秒...");

                // 這裡使用簡單的方式：每秒截圖一張並保存為影片
                // 注意：這不是真正的影片錄製，而是連續截圖
                // 如果需要真正的影片錄製，需要使用更複雜的庫如 FFmpeg
                await RecordVideoAsync((double)numRecordDuration.Value);
            }
            else
            {
                // 停止錄影
                isRecording = false;
                btnRecord!.Text = "開始錄影";
                btnCapture!.Enabled = true;
                numRecordDuration!.Enabled = true;
                UpdateStatus("錄影已停止");
            }
        }

        private async Task RecordVideoAsync(double durationSeconds)
        {
            try
            {
                string directory = Path.GetDirectoryName(currentRecordPath!)!;
                string baseFileName = Path.GetFileNameWithoutExtension(currentRecordPath!);
                int frameCount = 0;
                int totalFrames = (int)(durationSeconds * 10); // 每秒10幀
                double interval = 100; // 每100毫秒一幀
                DateTime startTime = DateTime.Now;
                remainingSeconds = durationSeconds;
                timerCountdown?.Start();

                for (int i = 0; i < totalFrames && isRecording; i++)
                {
                    if (pictureBox?.Image != null)
                    {
                        string framePath = Path.Combine(directory, $"{baseFileName}_frame_{frameCount:D6}.jpg");
                        pictureBox.Image.Save(framePath, ImageFormat.Jpeg);
                        frameCount++;
                    }
                    
                    // 計算剩餘時間（基於實際經過的時間）
                    double elapsed = (DateTime.Now - startTime).TotalSeconds;
                    remainingSeconds = Math.Max(0, durationSeconds - elapsed);
                    
                    await Task.Delay((int)interval);
                }

                timerCountdown?.Stop();
                remainingSeconds = 0;

                if (isRecording)
                {
                    UpdateStatus($"錄影完成：已儲存 {frameCount} 幀至 {directory}");
                    MessageBox.Show($"錄影完成！\n已儲存 {frameCount} 幀至：\n{directory}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"錄影時發生錯誤：{ex.Message}");
                MessageBox.Show($"錄影時發生錯誤：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isRecording = false;
                btnRecord!.Text = "開始錄影";
                btnCapture!.Enabled = true;
                numRecordDuration!.Enabled = true;
                timerCountdown?.Stop();
                if (lblCountdown != null)
                {
                    lblCountdown.Text = "";
                }
            }
        }

        private string GetTimestampedDirectory()
        {
            // 確保輸出目錄存在
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory!);
            }

            // 生成時間標籤目錄名稱
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseDirName = timestamp;
            string fullPath = Path.Combine(outputDirectory!, baseDirName);
            int counter = 0;

            // 如果目錄已存在，加上 _1, _2, _3...
            while (Directory.Exists(fullPath))
            {
                counter++;
                string newDirName = $"{baseDirName}_{counter}";
                fullPath = Path.Combine(outputDirectory!, newDirName);
            }

            // 創建目錄
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        private void UpdateStatus(string message)
        {
            if (lblStatus != null)
            {
                if (lblStatus.InvokeRequired)
                {
                    lblStatus.Invoke(new Action(() => lblStatus.Text = $"狀態：{message}"));
                }
                else
                {
                    lblStatus.Text = $"狀態：{message}";
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 停止計時器
            timerClock?.Stop();
            timerCountdown?.Stop();
            
            // 停止相機
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();
                videoSource = null;
            }
            
            // 儲存設定
            if (settings != null)
            {
                if (numCaptureDelay != null)
                {
                    settings.CaptureDelay = numCaptureDelay.Value;
                }
                if (numRecordDuration != null)
                {
                    settings.RecordDuration = numRecordDuration.Value;
                }
                settings.OutputDirectory = outputDirectory ?? settings.OutputDirectory;
                settings.Save();
            }
            
            base.OnFormClosing(e);
        }
    }
}
