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
        private Bitmap? currentFrame; // 當前畫面快照
        private readonly object frameLock = new object(); // 畫面鎖定物件
        private Button? btnConnect;
        private Button? btnCapture;
        private Button? btnRecord;
        private Button? btnSelectDirectory;
        private NumericUpDown? numCaptureDelay;
        private NumericUpDown? numRecordDuration;
        private NumericUpDown? numBurstCount;
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
                if (numBurstCount != null)
                {
                    numBurstCount.Value = settings.BurstCount;
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
            // 現代配色方案
            Color primaryColor = Color.FromArgb(66, 133, 244);      // Google Blue
            Color secondaryColor = Color.FromArgb(52, 152, 219);    // 次要藍色
            Color successColor = Color.FromArgb(46, 204, 113);     // 成功綠色
            Color dangerColor = Color.FromArgb(231, 76, 60);        // 危險紅色
            Color backgroundColor = Color.FromArgb(245, 247, 250);  // 淺灰背景
            Color cardColor = Color.White;                          // 卡片白色
            Color textPrimary = Color.FromArgb(44, 62, 80);         // 深灰文字
            Color textSecondary = Color.FromArgb(127, 140, 141);    // 淺灰文字

            this.Text = "📷 相機應用程式";
            this.Size = new Size(1000, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = backgroundColor;
            this.Font = new Font("Microsoft YaHei UI", 9F);

            int padding = 15;
            int cardSpacing = 15;
            int currentY = padding;

            // ========== 頂部控制面板 ==========
            var topPanel = new Panel
            {
                Location = new Point(padding, currentY),
                Size = new Size(this.Width - padding * 2, 60),
                BackColor = cardColor,
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(topPanel);

            // 相機選擇標籤
            var lblCamera = new Label
            {
                Text = "📹 選擇相機",
                Location = new Point(15, 18),
                Size = new Size(100, 25),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = textPrimary
            };
            topPanel.Controls.Add(lblCamera);

            // 相機下拉選單
            cmbCameras = new ComboBox
            {
                Location = new Point(120, 15),
                Size = new Size(350, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            topPanel.Controls.Add(cmbCameras);

            // 連接按鈕
            btnConnect = new Button
            {
                Text = "🔌 連接相機",
                Location = new Point(485, 15),
                Size = new Size(130, 30),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand
            };
            btnConnect.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 152, 219);
            btnConnect.FlatAppearance.MouseDownBackColor = Color.FromArgb(41, 128, 185);
            btnConnect.Click += BtnConnect_Click;
            topPanel.Controls.Add(btnConnect);

            currentY += topPanel.Height + cardSpacing;

            // ========== 預覽區域 ==========
            var previewPanel = new Panel
            {
                Location = new Point(padding, currentY),
                Size = new Size(640, 480),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(previewPanel);

            pictureBox = new PictureBox
            {
                Location = new Point(0, 0),
                Size = new Size(640, 480),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            previewPanel.Controls.Add(pictureBox);

            // ========== 右側控制面板 ==========
            int rightPanelX = padding + 640 + cardSpacing;
            var controlPanel = new Panel
            {
                Location = new Point(rightPanelX, currentY),
                Size = new Size(this.Width - rightPanelX - padding, 480),
                BackColor = cardColor,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(20)
            };
            this.Controls.Add(controlPanel);

            int controlY = 20;

            // 拍照設定組
            var captureGroupLabel = new Label
            {
                Text = "📸 拍照設定",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 25),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = textPrimary
            };
            controlPanel.Controls.Add(captureGroupLabel);
            controlY += 35;

            // 拍照延遲
            var lblCaptureDelay = new Label
            {
                Text = "延遲時間（秒）",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary
            };
            controlPanel.Controls.Add(lblCaptureDelay);
            controlY += 22;

            numCaptureDelay = new NumericUpDown
            {
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 28),
                Minimum = 0,
                Maximum = 60,
                Value = 0,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Font = new Font("Microsoft YaHei UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            numCaptureDelay.ValueChanged += NumCaptureDelay_ValueChanged;
            controlPanel.Controls.Add(numCaptureDelay);
            controlY += 45;

            // 連拍數量
            var lblBurstCount = new Label
            {
                Text = "連拍數量（張/秒）",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary
            };
            controlPanel.Controls.Add(lblBurstCount);
            controlY += 22;

            numBurstCount = new NumericUpDown
            {
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 28),
                Minimum = 1,
                Maximum = 30,
                Value = 1,
                DecimalPlaces = 0,
                Increment = 1,
                Font = new Font("Microsoft YaHei UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            numBurstCount.ValueChanged += NumBurstCount_ValueChanged;
            controlPanel.Controls.Add(numBurstCount);
            controlY += 50;

            // 錄影設定組
            var recordGroupLabel = new Label
            {
                Text = "🎥 錄影設定",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 25),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = textPrimary
            };
            controlPanel.Controls.Add(recordGroupLabel);
            controlY += 35;

            // 錄影時長
            var lblRecordDuration = new Label
            {
                Text = "錄影時長（秒）",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary
            };
            controlPanel.Controls.Add(lblRecordDuration);
            controlY += 22;

            numRecordDuration = new NumericUpDown
            {
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 28),
                Minimum = 1,
                Maximum = 300,
                Value = 10,
                DecimalPlaces = 1,
                Increment = 1,
                Font = new Font("Microsoft YaHei UI", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            numRecordDuration.ValueChanged += NumRecordDuration_ValueChanged;
            controlPanel.Controls.Add(numRecordDuration);
            controlY += 50;

            // 操作按鈕組
            var actionGroupLabel = new Label
            {
                Text = "⚡ 操作",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 25),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = textPrimary
            };
            controlPanel.Controls.Add(actionGroupLabel);
            controlY += 35;

            // 拍照按鈕
            btnCapture = new Button
            {
                Text = "📷 拍照",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 45),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                BackColor = successColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnCapture.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 174, 96);
            btnCapture.FlatAppearance.MouseDownBackColor = Color.FromArgb(34, 153, 84);
            btnCapture.Click += BtnCapture_Click;
            controlPanel.Controls.Add(btnCapture);
            controlY += 55;

            // 錄影按鈕
            btnRecord = new Button
            {
                Text = "🎬 開始錄影",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 45),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                BackColor = dangerColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnRecord.FlatAppearance.MouseOverBackColor = Color.FromArgb(192, 57, 43);
            btnRecord.FlatAppearance.MouseDownBackColor = Color.FromArgb(169, 50, 38);
            btnRecord.Click += BtnRecord_Click;
            controlPanel.Controls.Add(btnRecord);
            controlY += 55;

            // 選擇目錄按鈕
            btnSelectDirectory = new Button
            {
                Text = "📁 選擇目錄",
                Location = new Point(0, controlY),
                Size = new Size(controlPanel.Width - 40, 38),
                Font = new Font("Microsoft YaHei UI", 9F),
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand
            };
            btnSelectDirectory.FlatAppearance.MouseOverBackColor = Color.FromArgb(127, 140, 141);
            btnSelectDirectory.FlatAppearance.MouseDownBackColor = Color.FromArgb(108, 122, 125);
            btnSelectDirectory.Click += BtnSelectDirectory_Click;
            controlPanel.Controls.Add(btnSelectDirectory);

            currentY += 480 + cardSpacing;

            // ========== 底部狀態面板 ==========
            var statusPanel = new Panel
            {
                Location = new Point(padding, currentY),
                Size = new Size(this.Width - padding * 2, 120),
                BackColor = cardColor,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(20, 15, 20, 15)
            };
            this.Controls.Add(statusPanel);

            // 狀態標籤
            lblStatus = new Label
            {
                Text = "● 狀態：未連接",
                Location = new Point(0, 5),
                Size = new Size(statusPanel.Width - 40, 25),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                ForeColor = textSecondary
            };
            statusPanel.Controls.Add(lblStatus);

            // 輸出目錄標籤
            lblOutputDir = new Label
            {
                Text = $"📂 輸出目錄：{outputDirectory}",
                Location = new Point(0, 35),
                Size = new Size(statusPanel.Width - 40, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary
            };
            statusPanel.Controls.Add(lblOutputDir);

            // 當前時間標籤
            lblCurrentTime = new Label
            {
                Text = $"🕐 {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Location = new Point(0, 60),
                Size = new Size(300, 25),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = primaryColor
            };
            statusPanel.Controls.Add(lblCurrentTime);

            // 倒數計時標籤
            lblCountdown = new Label
            {
                Text = "",
                Location = new Point(320, 60),
                Size = new Size(300, 25),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = dangerColor
            };
            statusPanel.Controls.Add(lblCountdown);
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

        private void NumBurstCount_ValueChanged(object? sender, EventArgs e)
        {
            if (settings != null && numBurstCount != null)
            {
                settings.BurstCount = (int)numBurstCount.Value;
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
                    btnConnect!.Text = "🔌 連接相機";
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
                    btnConnect!.Text = "🔌 斷開連接";
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
                // 更新當前畫面快照
                lock (frameLock)
                {
                    currentFrame?.Dispose();
                    currentFrame = (Bitmap)eventArgs.Frame.Clone();
                }

                // 更新預覽畫面
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
            int burstCount = (int)(numBurstCount?.Value ?? 1);
            
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
                Bitmap? frameToSave = null;
                
                // 從當前畫面快照獲取最新畫面
                lock (frameLock)
                {
                    if (currentFrame != null)
                    {
                        frameToSave = (Bitmap)currentFrame.Clone();
                    }
                }

                if (frameToSave == null && pictureBox?.Image != null)
                {
                    // 如果沒有快照，使用預覽畫面
                    frameToSave = (Bitmap)pictureBox.Image.Clone();
                }

                if (frameToSave != null)
                {
                    string directory = GetTimestampedDirectory();
                    DateTime startTime = DateTime.Now;
                    int successCount = 0;
                    int totalCount = burstCount;

                    if (burstCount > 1)
                    {
                        // 連拍模式：在一秒內拍攝多張照片
                        UpdateStatus($"開始連拍 {burstCount} 張照片...");
                        double interval = 1000.0 / burstCount; // 每張照片的間隔時間（毫秒）
                        
                        for (int i = 0; i < burstCount && isCapturing; i++)
                        {
                            Bitmap? currentFrameToSave = null;
                            
                            // 每次拍照都獲取最新的畫面
                            lock (frameLock)
                            {
                                if (currentFrame != null)
                                {
                                    currentFrameToSave = (Bitmap)currentFrame.Clone();
                                }
                            }

                            if (currentFrameToSave == null && pictureBox?.Image != null)
                            {
                                currentFrameToSave = (Bitmap)pictureBox.Image.Clone();
                            }

                            if (currentFrameToSave != null)
                            {
                                // 使用毫秒時間戳和序號確保檔名唯一
                                DateTime now = DateTime.Now;
                                string fileName = $"photo_{now:yyyyMMdd_HHmmss}_{now.Millisecond:D3}_{i + 1:D2}.jpg";
                                string filePath = Path.Combine(directory, fileName);

                                try
                                {
                                    currentFrameToSave.Save(filePath, ImageFormat.Jpeg);
                                    successCount++;
                                    
                                    if (lblCountdown != null)
                                    {
                                        lblCountdown.Text = $"連拍進度：{i + 1}/{burstCount}";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"儲存第 {i + 1} 張照片失敗：{ex.Message}");
                                }
                                finally
                                {
                                    currentFrameToSave.Dispose();
                                }
                            }

                            // 如果不是最後一張，等待間隔時間
                            if (i < burstCount - 1)
                            {
                                await Task.Delay((int)interval);
                            }
                        }

                        frameToSave.Dispose();

                        UpdateStatus($"連拍完成：成功儲存 {successCount}/{totalCount} 張照片至 {directory}");
                        MessageBox.Show($"連拍完成！\n成功儲存 {successCount}/{totalCount} 張照片至：\n{directory}", 
                            "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        // 單張拍照模式
                        string fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                        string filePath = Path.Combine(directory, fileName);

                        frameToSave.Save(filePath, ImageFormat.Jpeg);
                        frameToSave.Dispose();
                        UpdateStatus($"照片已儲存：{filePath}");
                        MessageBox.Show($"照片已儲存至：\n{filePath}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
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
                btnRecord!.Text = "⏹ 停止錄影";
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
                btnRecord!.Text = "🎬 開始錄影";
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
                btnRecord!.Text = "🎬 開始錄影";
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
                // 根據狀態訊息決定指示器顏色
                string indicator = "●";
                Color statusColor = Color.FromArgb(127, 140, 141); // 預設灰色
                
                if (message.Contains("已連接") || message.Contains("成功") || message.Contains("完成"))
                {
                    indicator = "🟢";
                    statusColor = Color.FromArgb(46, 204, 113); // 成功綠色
                }
                else if (message.Contains("錯誤") || message.Contains("失敗") || message.Contains("停止"))
                {
                    indicator = "🔴";
                    statusColor = Color.FromArgb(231, 76, 60); // 錯誤紅色
                }
                else if (message.Contains("連接") || message.Contains("開始"))
                {
                    indicator = "🟡";
                    statusColor = Color.FromArgb(241, 196, 15); // 警告黃色
                }
                else if (message.Contains("未連接") || message.Contains("未偵測"))
                {
                    indicator = "⚪";
                    statusColor = Color.FromArgb(127, 140, 141); // 灰色
                }

                if (lblStatus.InvokeRequired)
                {
                    lblStatus.Invoke(new Action(() =>
                    {
                        lblStatus.Text = $"{indicator} 狀態：{message}";
                        lblStatus.ForeColor = statusColor;
                    }));
                }
                else
                {
                    lblStatus.Text = $"{indicator} 狀態：{message}";
                    lblStatus.ForeColor = statusColor;
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
            
            // 釋放畫面快照
            lock (frameLock)
            {
                currentFrame?.Dispose();
                currentFrame = null;
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
                if (numBurstCount != null)
                {
                    settings.BurstCount = (int)numBurstCount.Value;
                }
                settings.OutputDirectory = outputDirectory ?? settings.OutputDirectory;
                settings.Save();
            }
            
            base.OnFormClosing(e);
        }
    }
}
