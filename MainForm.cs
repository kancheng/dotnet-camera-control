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
        private Panel? topPanel;
        private Panel? previewPanel;
        private Panel? controlPanel;
        private Panel? statusPanel;
        private Panel? captureCard;
        private Panel? burstCard;
        private Panel? recordCard;
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
            // Material Design 配色方案
            Color primaryColor = Color.FromArgb(33, 150, 243);      // Material Blue 500
            Color primaryDark = Color.FromArgb(25, 118, 210);       // Material Blue 700
            Color primaryLight = Color.FromArgb(66, 165, 245);      // Material Blue 400
            Color accentColor = Color.FromArgb(76, 175, 80);        // Material Green 500
            Color accentDark = Color.FromArgb(56, 142, 60);         // Material Green 700
            Color errorColor = Color.FromArgb(244, 67, 54);         // Material Red 500
            Color errorDark = Color.FromArgb(211, 47, 47);          // Material Red 700
            Color backgroundColor = Color.FromArgb(250, 250, 250);   // Material Grey 50
            Color surfaceColor = Color.White;                       // Material White
            Color dividerColor = Color.FromArgb(224, 224, 224);     // Material Grey 300
            Color textPrimary = Color.FromArgb(33, 33, 33);         // Material Grey 900
            Color textSecondary = Color.FromArgb(117, 117, 117);   // Material Grey 600
            Color textHint = Color.FromArgb(158, 158, 158);         // Material Grey 500

            this.Text = "📷 相機應用程式";
            this.Size = new Size(1100, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = backgroundColor;
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.MinimumSize = new Size(1000, 700);
            
            // 添加響應式事件處理
            this.Resize += MainForm_Resize;
            this.ResizeEnd += MainForm_ResizeEnd;

            // 統一的間距系統（8px 網格）
            int spacing = 16;
            int padding = 20;
            int currentY = padding;

            // ========== 頂部工具欄 ==========
            topPanel = new Panel
            {
                Location = new Point(padding, currentY),
                Size = new Size(this.Width - padding * 2, 70),
                BackColor = surfaceColor,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(topPanel);

            // 相機選擇標籤
            var lblCamera = new Label
            {
                Text = "📹 選擇相機",
                Location = new Point(spacing, 22),
                Size = new Size(100, 26),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                ForeColor = textPrimary
            };
            topPanel.Controls.Add(lblCamera);

            // 相機下拉選單
            cmbCameras = new ComboBox
            {
                Location = new Point(120, 20),
                Size = new Size(380, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 9F),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            topPanel.Controls.Add(cmbCameras);

            // 連接按鈕
            btnConnect = new Button
            {
                Text = "🔌 連接相機",
                Location = new Point(520, 20),
                Size = new Size(140, 32),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnConnect.FlatAppearance.MouseOverBackColor = primaryDark;
            btnConnect.FlatAppearance.MouseDownBackColor = Color.FromArgb(13, 71, 161);
            btnConnect.Click += BtnConnect_Click;
            topPanel.Controls.Add(btnConnect);

            currentY += topPanel.Height + spacing;

            // ========== 主內容區域 ==========
            int previewWidth = 680;
            int previewHeight = 510;
            int controlPanelWidth = this.Width - padding * 3 - previewWidth;

            // 預覽區域
            previewPanel = new Panel
            {
                Location = new Point(padding, currentY),
                Size = new Size(previewWidth, previewHeight),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
                MinimumSize = new Size(400, 300)
            };
            this.Controls.Add(previewPanel);

            pictureBox = new PictureBox
            {
                Location = new Point(0, 0),
                Size = new Size(previewWidth, previewHeight),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                Dock = DockStyle.Fill
            };
            previewPanel.Controls.Add(pictureBox);

            // 右側控制面板
            controlPanel = new Panel
            {
                Location = new Point(padding + previewWidth + spacing, currentY),
                Size = new Size(controlPanelWidth, previewHeight),
                BackColor = surfaceColor,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(spacing),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                MinimumSize = new Size(300, 400),
                AutoScroll = true
            };
            this.Controls.Add(controlPanel);

            int controlY = spacing;
            int controlWidth = controlPanelWidth - spacing * 2;

            // ========== 拍照設定卡片 ==========
            captureCard = CreateCard(controlPanel, 0, controlY, controlWidth, 140, "📸 拍照設定", textPrimary);
            captureCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            controlY = spacing + 24;

            // 拍照延遲
            var lblCaptureDelay = new Label
            {
                Text = "延遲時間（秒）",
                Location = new Point(spacing, controlY),
                Size = new Size(controlWidth - spacing * 2, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            captureCard.Controls.Add(lblCaptureDelay);
            controlY += 24;

            numCaptureDelay = new NumericUpDown
            {
                Location = new Point(spacing, controlY),
                Size = new Size(controlWidth - spacing * 2, 32),
                Minimum = 0,
                Maximum = 60,
                Value = 0,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            numCaptureDelay.ValueChanged += NumCaptureDelay_ValueChanged;
            captureCard.Controls.Add(numCaptureDelay);
            controlY = captureCard.Bottom + spacing;

            // ========== 連拍設定卡片 ==========
            burstCard = CreateCard(controlPanel, 0, controlY, controlWidth, 110, "⚡ 連拍模式", primaryColor);
            burstCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            controlY = spacing + 24;

            var lblBurstCount = new Label
            {
                Text = "1 秒內拍攝張數",
                Location = new Point(spacing, controlY),
                Size = new Size(controlWidth - spacing * 2, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            burstCard.Controls.Add(lblBurstCount);
            controlY += 24;

            numBurstCount = new NumericUpDown
            {
                Location = new Point(spacing, controlY),
                Size = new Size(controlWidth - spacing * 2, 32),
                Minimum = 1,
                Maximum = 30,
                Value = 1,
                DecimalPlaces = 0,
                Increment = 1,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            numBurstCount.ValueChanged += NumBurstCount_ValueChanged;
            burstCard.Controls.Add(numBurstCount);
            controlY = burstCard.Bottom + spacing;

            // ========== 錄影設定卡片 ==========
            recordCard = CreateCard(controlPanel, 0, controlY, controlWidth, 120, "🎥 錄影設定", textPrimary);
            recordCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            controlY = spacing + 24;

            var lblRecordDuration = new Label
            {
                Text = "錄影時長（秒）",
                Location = new Point(spacing, controlY),
                Size = new Size(controlWidth - spacing * 2, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            recordCard.Controls.Add(lblRecordDuration);
            controlY += 24;

            numRecordDuration = new NumericUpDown
            {
                Location = new Point(spacing, controlY),
                Size = new Size(controlWidth - spacing * 2, 32),
                Minimum = 1,
                Maximum = 300,
                Value = 10,
                DecimalPlaces = 1,
                Increment = 1,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            numRecordDuration.ValueChanged += NumRecordDuration_ValueChanged;
            recordCard.Controls.Add(numRecordDuration);
            controlY = recordCard.Bottom + spacing;

            // ========== 操作按鈕區域 ==========
            int buttonY = controlY;
            int buttonHeight = 48;
            int buttonSpacing = 12;

            // 拍照按鈕
            btnCapture = new Button
            {
                Text = "📷 拍照",
                Location = new Point(0, buttonY),
                Size = new Size(controlWidth, buttonHeight),
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Enabled = false,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            btnCapture.FlatAppearance.MouseOverBackColor = accentDark;
            btnCapture.FlatAppearance.MouseDownBackColor = Color.FromArgb(46, 125, 50);
            btnCapture.Click += BtnCapture_Click;
            controlPanel.Controls.Add(btnCapture);
            buttonY += buttonHeight + buttonSpacing;

            // 錄影按鈕
            btnRecord = new Button
            {
                Text = "🎬 開始錄影",
                Location = new Point(0, buttonY),
                Size = new Size(controlWidth, buttonHeight),
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                BackColor = errorColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Enabled = false,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            btnRecord.FlatAppearance.MouseOverBackColor = errorDark;
            btnRecord.FlatAppearance.MouseDownBackColor = Color.FromArgb(183, 28, 28);
            btnRecord.Click += BtnRecord_Click;
            controlPanel.Controls.Add(btnRecord);
            buttonY += buttonHeight + buttonSpacing;

            // 選擇目錄按鈕
            btnSelectDirectory = new Button
            {
                Text = "📁 選擇目錄",
                Location = new Point(0, buttonY),
                Size = new Size(controlWidth, 40),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                BackColor = Color.FromArgb(158, 158, 158),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            btnSelectDirectory.FlatAppearance.MouseOverBackColor = Color.FromArgb(117, 117, 117);
            btnSelectDirectory.FlatAppearance.MouseDownBackColor = Color.FromArgb(97, 97, 97);
            btnSelectDirectory.Click += BtnSelectDirectory_Click;
            controlPanel.Controls.Add(btnSelectDirectory);

            currentY += previewHeight + spacing;

            // ========== 底部狀態欄 ==========
            statusPanel = new Panel
            {
                Location = new Point(padding, currentY),
                Size = new Size(this.Width - padding * 2, 100),
                BackColor = surfaceColor,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(spacing),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(statusPanel);

            // 狀態標籤
            lblStatus = new Label
            {
                Text = "● 狀態：未連接",
                Location = new Point(0, 8),
                Size = new Size(statusPanel.Width - spacing * 2, 24),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                ForeColor = textSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };
            statusPanel.Controls.Add(lblStatus);

            // 輸出目錄標籤
            lblOutputDir = new Label
            {
                Text = $"📂 輸出目錄：{outputDirectory}",
                Location = new Point(0, 36),
                Size = new Size(statusPanel.Width - spacing * 2, 20),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = textSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };
            statusPanel.Controls.Add(lblOutputDir);

            // 當前時間和倒數計時（並排顯示）
            lblCurrentTime = new Label
            {
                Text = $"🕐 {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Location = new Point(0, 60),
                Size = new Size((statusPanel.Width - spacing * 2) / 2 - 8, 24),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = primaryColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
            };
            statusPanel.Controls.Add(lblCurrentTime);

            lblCountdown = new Label
            {
                Text = "",
                Location = new Point((statusPanel.Width - spacing * 2) / 2 + 8, 60),
                Size = new Size((statusPanel.Width - spacing * 2) / 2 - 8, 24),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = errorColor,
                TextAlign = ContentAlignment.TopRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
            };
            statusPanel.Controls.Add(lblCountdown);
        }

        // 響應式布局調整
        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized) return;
            
            AdjustLayout();
        }

        private void MainForm_ResizeEnd(object? sender, EventArgs e)
        {
            AdjustLayout();
        }

        private void AdjustLayout()
        {
            if (topPanel == null || previewPanel == null || controlPanel == null || statusPanel == null)
                return;

            int padding = 20;
            int spacing = 16;
            int topPanelHeight = 70;
            int statusPanelHeight = 100;

            try
            {
                // 調整頂部面板
                topPanel.Location = new Point(padding, padding);
                topPanel.Width = this.ClientSize.Width - padding * 2;

                // 調整主內容區域
                int contentY = padding + topPanelHeight + spacing;
                int contentHeight = this.ClientSize.Height - contentY - statusPanelHeight - spacing - padding;
                
                // 確保最小高度
                if (contentHeight < 300) contentHeight = 300;

                // 計算預覽和控制面板寬度
                int previewWidth = Math.Max(400, (int)((this.ClientSize.Width - padding * 3 - spacing) * 0.6));
                int controlPanelWidth = this.ClientSize.Width - padding * 3 - previewWidth - spacing;

                // 確保控制面板最小寬度
                if (controlPanelWidth < 300)
                {
                    controlPanelWidth = 300;
                    previewWidth = this.ClientSize.Width - padding * 3 - controlPanelWidth - spacing;
                }

                // 調整預覽面板
                previewPanel.Location = new Point(padding, contentY);
                previewPanel.Size = new Size(previewWidth, contentHeight);

                // 調整控制面板
                controlPanel.Location = new Point(padding + previewWidth + spacing, contentY);
                controlPanel.Size = new Size(controlPanelWidth, contentHeight);

                // 調整控制面板內的卡片寬度（Anchor 會自動處理，這裡只是確保）
                int controlWidth = controlPanelWidth - spacing * 2;
                if (captureCard != null && captureCard.Width != controlWidth)
                {
                    captureCard.Width = controlWidth;
                }
                if (burstCard != null && burstCard.Width != controlWidth)
                {
                    burstCard.Width = controlWidth;
                }
                if (recordCard != null && recordCard.Width != controlWidth)
                {
                    recordCard.Width = controlWidth;
                }

                // 調整底部狀態面板
                statusPanel.Location = new Point(padding, this.ClientSize.Height - statusPanelHeight - padding);
                statusPanel.Width = this.ClientSize.Width - padding * 2;

                // 調整狀態面板內的標籤
                if (lblStatus != null)
                {
                    lblStatus.Width = statusPanel.Width - spacing * 2;
                }
                if (lblOutputDir != null)
                {
                    lblOutputDir.Width = statusPanel.Width - spacing * 2;
                }
                if (lblCurrentTime != null && lblCountdown != null)
                {
                    int halfWidth = (statusPanel.Width - spacing * 2) / 2 - 8;
                    lblCurrentTime.Width = halfWidth;
                    lblCountdown.Location = new Point(halfWidth + 16, 60);
                    lblCountdown.Width = halfWidth;
                }

                // 調整頂部面板內的控件
                if (cmbCameras != null && btnConnect != null)
                {
                    int availableWidth = topPanel.Width - spacing * 3 - 100; // 標籤寬度
                    int comboWidth = Math.Max(200, (int)(availableWidth * 0.6));
                    int buttonWidth = 140;
                    
                    cmbCameras.Width = comboWidth;
                    btnConnect.Location = new Point(topPanel.Width - buttonWidth - spacing, 20);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"調整布局時發生錯誤：{ex.Message}");
            }
        }

        // 創建卡片控件的輔助方法
        private Panel CreateCard(Control parent, int x, int y, int width, int height, string title, Color titleColor)
        {
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(16)
            };
            parent.Controls.Add(card);

            // 標題
            var titleLabel = new Label
            {
                Text = title,
                Location = new Point(0, 0),
                Size = new Size(width - 32, 24),
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                ForeColor = titleColor
            };
            card.Controls.Add(titleLabel);

            return card;
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
                        UpdateStatus($"開始連拍模式：1 秒內拍攝 {burstCount} 張照片...");
                        DateTime burstStartTime = DateTime.Now;
                        double totalDuration = 1000.0; // 總共 1 秒
                        double interval = totalDuration / burstCount; // 每張照片的間隔時間（毫秒）
                        
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
                                // 計算從開始連拍算起的時間（秒）
                                DateTime now = DateTime.Now;
                                double elapsedSeconds = (now - burstStartTime).TotalSeconds;
                                
                                // 文件名格式：burst_{開始時間}_{經過秒數}sec_{第幾張}of{總數}.jpg
                                // 例如：burst_20240101_120000_0.123sec_01of05.jpg
                                string fileName = $"burst_{burstStartTime:yyyyMMdd_HHmmss}_{elapsedSeconds:F3}sec_{i + 1:D2}of{burstCount:D2}.jpg";
                                string filePath = Path.Combine(directory, fileName);

                                try
                                {
                                    currentFrameToSave.Save(filePath, ImageFormat.Jpeg);
                                    successCount++;
                                    
                                    // 更新進度顯示
                                    if (lblCountdown != null)
                                    {
                                        double elapsed = (DateTime.Now - burstStartTime).TotalMilliseconds;
                                        double remaining = Math.Max(0, totalDuration - elapsed);
                                        lblCountdown.Text = $"連拍進度：{i + 1}/{burstCount} (剩餘 {remaining:F0}ms)";
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

                            // 計算下一張照片應該拍攝的時間點，確保在 1 秒內完成
                            double elapsedTime = (DateTime.Now - burstStartTime).TotalMilliseconds;
                            double nextShotTime = (i + 1) * interval;
                            double waitTime = Math.Max(0, nextShotTime - elapsedTime);

                            // 如果不是最後一張，等待到正確的時間點
                            if (i < burstCount - 1 && waitTime > 0)
                            {
                                await Task.Delay((int)waitTime);
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
            // 檢查相機是否還連接著
            if (videoSource != null && videoSource.IsRunning)
            {
                // 取消關閉事件，顯示提示對話框
                e.Cancel = true;
                ShowCameraDisconnectDialog();
                return;
            }
            
            // 相機已斷開或未連接，執行正常關閉流程
            PerformCleanup();
        }

        private void ShowCameraDisconnectDialog()
        {
            // 創建自定義對話框
            var dialog = new Form
            {
                Text = "⚠️ 相機連接提示",
                Size = new Size(450, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                TopMost = true
            };

            // 提示訊息標籤
            var lblMessage = new Label
            {
                Text = "檢測到相機仍在連接狀態。\n\n請先斷開相機連接後再關閉程式，\n以確保資源正確釋放。",
                Location = new Point(20, 20),
                Size = new Size(400, 80),
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = Color.FromArgb(33, 33, 33)
            };
            dialog.Controls.Add(lblMessage);

            // 斷開並關閉按鈕
            var btnDisconnectAndClose = new Button
            {
                Text = "🔌 斷開相機並關閉程式",
                Location = new Point(20, 110),
                Size = new Size(200, 40),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(244, 67, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnDisconnectAndClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(211, 47, 47);
            btnDisconnectAndClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(183, 28, 28);
            btnDisconnectAndClose.Click += (s, e) =>
            {
                // 執行斷開和關閉操作
                DisconnectCameraAndClose();
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };
            dialog.Controls.Add(btnDisconnectAndClose);

            // 取消按鈕
            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(240, 110),
                Size = new Size(100, 40),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                BackColor = Color.FromArgb(158, 158, 158),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(117, 117, 117);
            btnCancel.FlatAppearance.MouseDownBackColor = Color.FromArgb(97, 97, 97);
            btnCancel.Click += (s, e) =>
            {
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
            };
            dialog.Controls.Add(btnCancel);

            // 設置對話框的接受和取消按鈕
            dialog.AcceptButton = btnDisconnectAndClose;
            dialog.CancelButton = btnCancel;

            // 顯示對話框
            dialog.ShowDialog(this);
        }

        private void DisconnectCameraAndClose()
        {
            try
            {
                // 停止計時器
                timerClock?.Stop();
                timerCountdown?.Stop();
                
                // 如果正在拍照或錄影，先停止
                if (isCapturing)
                {
                    isCapturing = false;
                }
                
                if (isRecording)
                {
                    isRecording = false;
                }
                
                // 斷開相機連接
                if (videoSource != null && videoSource.IsRunning)
                {
                    try
                    {
                        // 取消事件處理
                        videoSource.NewFrame -= VideoSource_NewFrame;
                        
                        // 停止相機
                        videoSource.SignalToStop();
                        
                        // 等待相機完全停止（最多等待 3 秒）
                        int waitCount = 0;
                        while (videoSource.IsRunning && waitCount < 30)
                        {
                            System.Threading.Thread.Sleep(100);
                            waitCount++;
                        }
                        
                        // 如果還在運行，強制等待
                        if (videoSource.IsRunning)
                        {
                            videoSource.WaitForStop();
                        }
                        
                        // 釋放資源
                        videoSource = null;
                        
                        System.Diagnostics.Debug.WriteLine("相機已成功斷開連接");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"斷開相機連接時發生錯誤：{ex.Message}");
                        
                        // 嘗試強制釋放
                        try
                        {
                            videoSource = null;
                        }
                        catch
                        {
                            // 忽略強制釋放時的錯誤
                        }
                    }
                }
                
                // 再次確認相機已斷開（雙重檢查）
                if (videoSource != null)
                {
                    try
                    {
                        if (videoSource.IsRunning)
                        {
                            videoSource.SignalToStop();
                            videoSource.WaitForStop();
                        }
                        videoSource = null;
                    }
                    catch
                    {
                        videoSource = null;
                    }
                }
                
                // 執行清理並關閉
                PerformCleanup();
                
                // 關閉應用程式
                Application.Exit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"斷開相機並關閉時發生錯誤：{ex.Message}");
                // 即使發生錯誤也嘗試關閉
                Application.Exit();
            }
        }

        private void PerformCleanup()
        {
            try
            {
                // 釋放畫面快照
                lock (frameLock)
                {
                    currentFrame?.Dispose();
                    currentFrame = null;
                }
                
                // 釋放預覽畫面
                if (pictureBox?.Image != null)
                {
                    var img = pictureBox.Image;
                    pictureBox.Image = null;
                    img.Dispose();
                }
                
                // 儲存設定
                if (settings != null)
                {
                    try
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"儲存設定時發生錯誤：{ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"執行清理時發生錯誤：{ex.Message}");
            }
        }
    }
}
