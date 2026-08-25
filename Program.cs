using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerScannerGUI
{
    // =========================================================================
    // 🔮 Touhou Character Display Component (Alice Margatroid / Touhou)
    // =========================================================================
    public class TouhouDisplayControl : Control
    {
        private System.Windows.Forms.Timer animTimer;
        private float animPhase = 0f;
        private bool isDancing = false;
        private string statusText = "Магия Алисы готова к поиску серверов! ✨ (ᗒᗣᗕ)՞";
        private List<Sparkle> sparkles = new List<Sparkle>();
        private Random rnd = new Random();

        private Image imgIdle = null;
        private Image imgDance = null;
        private PictureBox picAvatar;
        private Panel pnlBubble;
        private Label lblBubbleTitle;
        private Label lblBubbleText;

        public bool IsDancing
        {
            get { return isDancing; }
            set
            {
                isDancing = value;
                statusText = isDancing ? "Алиса танцует! Гримуар активирован, сканирую сеть... ♪ 🔮" : "Алиса отдыхает. Сканирование завершено ✨";
                UpdateAvatarImage();
                if (lblBubbleText != null && !lblBubbleText.IsDisposed)
                    lblBubbleText.Text = statusText;
                Invalidate();
            }
        }

        public string StatusText
        {
            get { return statusText; }
            set
            {
                statusText = value;
                if (lblBubbleText != null && !lblBubbleText.IsDisposed)
                    lblBubbleText.Text = value;
                Invalidate();
            }
        }

        private class Sparkle
        {
            public float X, Y, SpeedY, SpeedX, Alpha, Size;
            public string Symbol;
        }

        public TouhouDisplayControl()
        {
            DoubleBuffered = true;
            Height = 175;
            Width = 550;
            BackColor = Color.FromArgb(16, 20, 32);

            // Загрузка изображений
            LoadImages();

            // PictureBox для отображения аватара
            picAvatar = new PictureBox
            {
                Size = new Size(130, 130),
                Location = new Point(20, 22),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            Controls.Add(picAvatar);
            UpdateAvatarImage();

            // Облачко диалога
            pnlBubble = new Panel
            {
                Location = new Point(170, 20),
                Size = new Size(350, 132),
                BackColor = Color.FromArgb(28, 34, 52)
            };
            pnlBubble.Paint += PnlBubble_Paint;

            lblBubbleTitle = new Label
            {
                Text = "🔮 Touhou Assistant (Alice Margatroid)",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 128, 171),
                Location = new Point(14, 10),
                AutoSize = true
            };

            lblBubbleText = new Label
            {
                Text = statusText,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                ForeColor = Color.White,
                Location = new Point(14, 38),
                Size = new Size(320, 80)
            };

            pnlBubble.Controls.Add(lblBubbleTitle);
            pnlBubble.Controls.Add(lblBubbleText);
            Controls.Add(pnlBubble);

            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 35; // ~30 FPS
            animTimer.Tick += (s, e) =>
            {
                animPhase += isDancing ? 0.15f : 0.05f;
                if (animPhase > (float)Math.PI * 200) animPhase = 0;

                // Летающие звёздочки и блёстки
                if (isDancing && rnd.Next(0, 3) == 0 && sparkles.Count < 25)
                {
                    string[] symbols = new string[] { "✨", "★", "☆", "♪", "♫", "🌟", "⭐", "🔮" };
                    sparkles.Add(new Sparkle
                    {
                        X = 85 + rnd.Next(-55, 55),
                        Y = 120,
                        SpeedY = (float)(rnd.NextDouble() * 2.5 + 1.2),
                        SpeedX = (float)(rnd.NextDouble() * 1.8 - 0.9),
                        Alpha = 1.0f,
                        Size = rnd.Next(11, 19),
                        Symbol = symbols[rnd.Next(symbols.Length)]
                    });
                }

                for (int i = sparkles.Count - 1; i >= 0; i--)
                {
                    sparkles[i].Y -= sparkles[i].SpeedY;
                    sparkles[i].X += sparkles[i].SpeedX;
                    sparkles[i].Alpha -= 0.025f;
                    if (sparkles[i].Alpha <= 0 || sparkles[i].Y < 5)
                    {
                        sparkles.RemoveAt(i);
                    }
                }

                // Плавное покачивание при танце
                if (isDancing)
                {
                    int bounce = (int)(Math.Sin(animPhase * 2) * 5);
                    int sway = (int)(Math.Sin(animPhase * 1.3) * 3);
                    picAvatar.Location = new Point(20 + sway, 22 + bounce);
                }
                else
                {
                    // Лёгкое дыхание в покое
                    int breathe = (int)(Math.Sin(animPhase) * 2);
                    picAvatar.Location = new Point(20, 22 + breathe);
                }

                Invalidate();
            };
            animTimer.Start();
        }

        private void LoadImages()
        {
            try
            {
                // Поиск статичного кадра (idle) в папках assets/ и корневой
                string[] idleCandidates = new string[] {
                    "assets/touhou_static.gif", "assets/touhou_static.png", "assets/fumo.png",
                    "../assets/touhou_static.gif", "touhou_static.gif", "touhou_static.png", "fumo.png"
                };
                foreach (string path in idleCandidates)
                {
                    if (File.Exists(path))
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                        {
                            imgIdle = Image.FromStream(new MemoryStream(ReadAllBytes(fs)));
                        }
                        break;
                    }
                }

                // Поиск анимированного GIF (active) в папках assets/ и корневой
                string[] danceCandidates = new string[] {
                    "assets/touhou.gif", "assets/fumo.gif",
                    "../assets/touhou.gif", "touhou.gif", "fumo.gif"
                };
                foreach (string path in danceCandidates)
                {
                    if (File.Exists(path))
                    {
                        imgDance = Image.FromFile(path);
                        break;
                    }
                }

                // Если статичный кадр не найден, генерируем его из 1-го кадра GIF
                if (imgIdle == null && imgDance != null)
                {
                    Bitmap bmp = new Bitmap(imgDance.Width, imgDance.Height);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.DrawImage(imgDance, 0, 0, imgDance.Width, imgDance.Height);
                    }
                    imgIdle = bmp;
                }
            }
            catch { }
        }

        private byte[] ReadAllBytes(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private void UpdateAvatarImage()
        {
            if (isDancing && imgDance != null)
            {
                picAvatar.Image = imgDance;
            }
            else if (!isDancing && imgIdle != null)
            {
                picAvatar.Image = imgIdle;
            }
            else if (imgDance != null)
            {
                picAvatar.Image = imgDance;
            }
        }

        private void PnlBubble_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath cardPath = GetRoundRect(new Rectangle(0, 0, pnlBubble.Width - 1, pnlBubble.Height - 1), 14))
            {
                using (Pen borderPen = new Pen(Color.FromArgb(255, 105, 180), 1.5f))
                    g.DrawPath(borderPen, cardPath);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (pnlBubble != null)
            {
                pnlBubble.Width = Math.Max(200, Width - 195);
                if (lblBubbleText != null)
                    lblBubbleText.Width = pnlBubble.Width - 28;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Рамка карточки с неоновым свечением
            using (GraphicsPath cardPath = GetRoundRect(new Rectangle(2, 2, Width - 5, Height - 5), 14))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(16, 20, 32)))
                    g.FillPath(bgBrush, cardPath);

                Color glowColor = isDancing ? Color.FromArgb(255, 51, 119) : Color.FromArgb(70, 95, 145);
                using (Pen borderPen = new Pen(glowColor, isDancing ? 2.5f : 1.5f))
                    g.DrawPath(borderPen, cardPath);
            }

            // 2. Отрисовка летающих звёздочек
            foreach (var sp in sparkles)
            {
                int alphaVal = (int)(sp.Alpha * 255);
                if (alphaVal < 0) alphaVal = 0; if (alphaVal > 255) alphaVal = 255;
                using (Font spFont = new Font("Segoe UI Emoji", sp.Size, FontStyle.Bold))
                using (SolidBrush spBrush = new SolidBrush(Color.FromArgb(alphaVal, 255, 209, 102)))
                {
                    g.DrawString(sp.Symbol, spFont, spBrush, sp.X, sp.Y);
                }
            }
        }

        private GraphicsPath GetRoundRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (animTimer != null) { animTimer.Stop(); animTimer.Dispose(); }
                if (imgIdle != null) imgIdle.Dispose();
                if (imgDance != null) imgDance.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // =========================================================================
    // 🖥️ Main Application Window
    // =========================================================================
    public class MainForm : Form
    {
        private TextBox txtSingleIp;
        private TextBox txtPorts;
        private Button btnScanSingle;
        private Button btnLoadFile;
        private Button btnStartMassScan;
        private Button btnStopScan;
        private Button btnExportJson;
        private Button btnExportCsv;
        private Button btnExportExcel;
        private Label lblFileLoaded;
        private ProgressBar prgScan;
        private Label lblProgressStats;
        private DataGridView gridResults;
        private TouhouDisplayControl touhou;

        private NumericUpDown numThreads;
        private NumericUpDown numTimeout;
        private CheckBox chkBanner;
        private CheckBox chkRdns;
        private CheckBox chkExpandCidr;

        private List<string> loadedFileTargets = new List<string>();
        private List<ScanResultRow> scanResults = new List<ScanResultRow>();
        private CancellationTokenSource scanCts;
        private volatile bool isScanning = false;

        public class ScanResultRow
        {
            public string IP { get; set; }
            public int Port { get; set; }
            public string Service { get; set; }
            public string Status { get; set; }
            public double LatencyMs { get; set; }
            public string Hostname { get; set; }
            public string HttpStatus { get; set; }
            public string HttpServer { get; set; }
            public string HttpTitle { get; set; }
            public string Banner { get; set; }
        }

        public MainForm()
        {
            InitializeComponent();
            CheckAutoLoadTargets();
        }

        private void InitializeComponent()
        {
            Text = "⚡ ServerScanner Pro — Touhou Edition 🔮";
            Size = new Size(1040, 780);
            MinimumSize = new Size(900, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(14, 17, 27);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            // Верхняя панель: Алиса / Touhou
            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 185, Padding = new Padding(12, 8, 12, 4) };
            touhou = new TouhouDisplayControl { Dock = DockStyle.Fill };
            pnlTop.Controls.Add(touhou);

            // Панель управления и ввода
            Panel pnlControls = new Panel { Dock = DockStyle.Top, Height = 175, Padding = new Padding(14, 6, 14, 6), BackColor = Color.FromArgb(20, 24, 38) };

            // 1. Строка одиночного IP
            Label lblIp = new Label { Text = "🎯 Одиночный IP / CIDR:", Location = new Point(14, 14), AutoSize = true, ForeColor = Color.FromArgb(255, 180, 210), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtSingleIp = new TextBox { Text = "1.1.1.1", Location = new Point(190, 12), Width = 230, BackColor = Color.FromArgb(28, 34, 52), ForeColor = Color.White, Font = new Font("Consolas", 10.5f) };
            btnScanSingle = new Button { Text = "⚡ Сканировать этот IP", Location = new Point(430, 10), Width = 180, Height = 30, BackColor = Color.FromArgb(255, 51, 119), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            btnScanSingle.FlatAppearance.BorderSize = 0;
            btnScanSingle.Click += async (s, e) => await StartScanAsync(new List<string> { txtSingleIp.Text.Trim() });

            // 2. Кнопка массовой загрузки файла
            Label lblMass = new Label { Text = "📁 Массовая загрузка:", Location = new Point(14, 52), AutoSize = true, ForeColor = Color.FromArgb(0, 229, 255), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            btnLoadFile = new Button { Text = "📂 Выбрать файл с IP...", Location = new Point(190, 48), Width = 180, Height = 30, BackColor = Color.FromArgb(0, 180, 216), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            btnLoadFile.FlatAppearance.BorderSize = 0;
            btnLoadFile.Click += BtnLoadFile_Click;

            lblFileLoaded = new Label { Text = "Файл не выбран", Location = new Point(380, 54), AutoSize = true, ForeColor = Color.FromArgb(180, 190, 210) };

            btnStartMassScan = new Button { Text = "🚀 Запустить весь файл", Location = new Point(620, 48), Width = 175, Height = 30, BackColor = Color.FromArgb(6, 214, 160), ForeColor = Color.FromArgb(4, 39, 29), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand, Enabled = false };
            btnStartMassScan.FlatAppearance.BorderSize = 0;
            btnStartMassScan.Click += async (s, e) => await StartScanAsync(loadedFileTargets);

            btnStopScan = new Button { Text = "⛔ Стоп", Location = new Point(805, 48), Width = 80, Height = 30, BackColor = Color.FromArgb(239, 71, 111), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand, Enabled = false };
            btnStopScan.FlatAppearance.BorderSize = 0;
            btnStopScan.Click += (s, e) => StopScan();

            // 3. Порты и настройки
            Label lblPorts = new Label { Text = "🔌 Порты (порты/пресеты):", Location = new Point(14, 92), AutoSize = true, ForeColor = Color.FromArgb(255, 209, 102), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtPorts = new TextBox { Text = "80,443", Location = new Point(190, 90), Width = 160, BackColor = Color.FromArgb(28, 34, 52), ForeColor = Color.White, Font = new Font("Consolas", 9.5f) };

            // Кнопки пресетов
            Button btnPWeb = CreatePresetButton("Web", "80,443,8080,8443", 360, 89);
            Button btnPTop = CreatePresetButton("Top10", "21,22,25,80,110,443,3306,3389,5432,8080", 415, 89);
            Button btnPCommon = CreatePresetButton("Common", "21,22,80,443,3306,5432,6379,8080,8443", 475, 89);

            Label lblThr = new Label { Text = "Потоков:", Location = new Point(560, 93), AutoSize = true };
            numThreads = new NumericUpDown { Location = new Point(625, 90), Width = 55, Minimum = 1, Maximum = 200, Value = 40, BackColor = Color.FromArgb(28, 34, 52), ForeColor = Color.White };

            Label lblTo = new Label { Text = "Таймаут (с):", Location = new Point(695, 93), AutoSize = true };
            numTimeout = new NumericUpDown { Location = new Point(780, 90), Width = 55, Minimum = 0.1M, Maximum = 10M, DecimalPlaces = 1, Value = 1.0M, Increment = 0.2M, BackColor = Color.FromArgb(28, 34, 52), ForeColor = Color.White };

            // Чекбоксы разведки
            chkBanner = new CheckBox { Text = "HTTP & Banners", Location = new Point(190, 126), AutoSize = true, Checked = true, ForeColor = Color.FromArgb(220, 220, 240) };
            chkRdns = new CheckBox { Text = "Reverse DNS", Location = new Point(320, 126), AutoSize = true, Checked = true, ForeColor = Color.FromArgb(220, 220, 240) };
            chkExpandCidr = new CheckBox { Text = "Разворачивать CIDR (/24)", Location = new Point(440, 126), AutoSize = true, Checked = false, ForeColor = Color.FromArgb(220, 220, 240) };

            // Кнопки экспорта
            btnExportJson = new Button { Text = "💾 JSON", Location = new Point(720, 124), Width = 80, Height = 28, BackColor = Color.FromArgb(179, 71, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            btnExportJson.FlatAppearance.BorderSize = 0;
            btnExportJson.Click += (s, e) => ExportResults("json");

            btnExportCsv = new Button { Text = "📊 CSV", Location = new Point(810, 124), Width = 80, Height = 28, BackColor = Color.FromArgb(0, 150, 136), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            btnExportCsv.FlatAppearance.BorderSize = 0;
            btnExportCsv.Click += (s, e) => ExportResults("csv");

            btnExportExcel = new Button { Text = "📉 Excel", Location = new Point(900, 124), Width = 80, Height = 28, BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            btnExportExcel.FlatAppearance.BorderSize = 0;
            btnExportExcel.Click += (s, e) => ExportResults("xls");

            pnlControls.Controls.AddRange(new Control[] {
                lblIp, txtSingleIp, btnScanSingle,
                lblMass, btnLoadFile, lblFileLoaded, btnStartMassScan, btnStopScan,
                lblPorts, txtPorts, btnPWeb, btnPTop, btnPCommon,
                lblThr, numThreads, lblTo, numTimeout,
                chkBanner, chkRdns, chkExpandCidr,
                btnExportJson, btnExportCsv, btnExportExcel
            });

            // Панель прогресс-бара
            Panel pnlProgress = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(14, 6, 14, 6), BackColor = Color.FromArgb(14, 17, 27) };
            prgScan = new ProgressBar { Dock = DockStyle.Top, Height = 14, Style = ProgressBarStyle.Continuous };
            lblProgressStats = new Label { Dock = DockStyle.Bottom, Height = 22, Text = "Готов к запуску. Выберите IP или файл для сканирования.", ForeColor = Color.FromArgb(170, 185, 215) };
            pnlProgress.Controls.Add(lblProgressStats);
            pnlProgress.Controls.Add(prgScan);

            // Таблица результатов
            gridResults = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(14, 17, 27),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(32, 38, 56)
            };

            gridResults.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 34, 52);
            gridResults.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(255, 128, 171);
            gridResults.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            gridResults.ColumnHeadersHeight = 32;

            gridResults.DefaultCellStyle.BackColor = Color.FromArgb(18, 22, 34);
            gridResults.DefaultCellStyle.ForeColor = Color.White;
            gridResults.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 51, 119);
            gridResults.DefaultCellStyle.SelectionForeColor = Color.White;
            gridResults.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            gridResults.RowTemplate.Height = 28;

            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "Статус", FillWeight = 40 });
            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColIP", HeaderText = "IP Адрес", FillWeight = 65 });
            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColPort", HeaderText = "Порт", FillWeight = 35 });
            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColService", HeaderText = "Сервис", FillWeight = 50 });
            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColLatency", HeaderText = "Ping (ms)", FillWeight = 40 });
            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColHostname", HeaderText = "Reverse DNS", FillWeight = 85 });
            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColHttp", HeaderText = "HTTP / Server", FillWeight = 80 });
            gridResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColBanner", HeaderText = "Title / Banner", FillWeight = 110 });

            Controls.Add(gridResults);
            Controls.Add(pnlProgress);
            Controls.Add(pnlControls);
            Controls.Add(pnlTop);
        }

        private Button CreatePresetButton(string text, string portsVal, int x, int y)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(52, 24),
                BackColor = Color.FromArgb(38, 46, 70),
                ForeColor = Color.FromArgb(255, 209, 102),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => txtPorts.Text = portsVal;
            return btn;
        }

        private void CheckAutoLoadTargets()
        {
            string[] targetCandidates = new string[] {
                "data/targets.txt", "../data/targets.txt", "targets.txt"
            };
            foreach (string path in targetCandidates)
            {
                if (File.Exists(path))
                {
                    LoadTargetsFromFile(path);
                    break;
                }
            }
        }

        private void BtnLoadFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                ofd.Title = "Выберите файл со списком IP-адресов";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadTargetsFromFile(ofd.FileName);
                }
            }
        }

        private void LoadTargetsFromFile(string path)
        {
            try
            {
                string[] lines = File.ReadAllLines(path);
                loadedFileTargets.Clear();
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (!string.IsNullOrEmpty(t) && !t.StartsWith("#"))
                        loadedFileTargets.Add(t);
                }

                string name = Path.GetFileName(path);
                lblFileLoaded.Text = string.Format("✓ Загружен: {0} ({1} целей)", name, loadedFileTargets.Count);
                lblFileLoaded.ForeColor = Color.FromArgb(6, 214, 160);
                btnStartMassScan.Enabled = loadedFileTargets.Count > 0;
                touhou.StatusText = string.Format("Файл '{0}' готов ({1} целей)! Нажми 'Запустить весь файл' ~ ✨", name, loadedFileTargets.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения файла: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task StartScanAsync(List<string> rawTargets)
        {
            if (isScanning || rawTargets == null || rawTargets.Count == 0) return;

            List<string> expandedTargets = new List<string>();
            bool expandCidr = chkExpandCidr.Checked;
            foreach (string t in rawTargets)
            {
                if (expandCidr && t.Contains("/"))
                    expandedTargets.AddRange(ExpandCidr(t));
                else
                    expandedTargets.Add(t);
            }

            List<int> ports = ParsePorts(txtPorts.Text);
            if (ports.Count == 0) ports.Add(80);

            int maxThreads = (int)numThreads.Value;
            int timeoutMs = (int)(numTimeout.Value * 1000);
            bool grabBanner = chkBanner.Checked;
            bool doRdns = chkRdns.Checked;

            isScanning = true;
            scanCts = new CancellationTokenSource();
            btnScanSingle.Enabled = false;
            btnStartMassScan.Enabled = false;
            btnLoadFile.Enabled = false;
            btnStopScan.Enabled = true;
            gridResults.Rows.Clear();
            scanResults.Clear();

            touhou.IsDancing = true;

            int totalTasks = expandedTargets.Count * ports.Count;
            prgScan.Maximum = totalTasks;
            prgScan.Value = 0;

            int completedTasks = 0;
            int openCount = 0;
            DateTime startTime = DateTime.Now;

            var queue = new List<Tuple<string, int>>();
            foreach (var ip in expandedTargets)
                foreach (var p in ports)
                    queue.Add(Tuple.Create(ip, p));

            using (var sem = new SemaphoreSlim(maxThreads))
            {
                var tasks = new List<Task>();
                foreach (var item in queue)
                {
                    if (scanCts.IsCancellationRequested) break;
                    await sem.WaitAsync();

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            if (scanCts.IsCancellationRequested) return;

                            var res = await ScanPortAsync(item.Item1, item.Item2, timeoutMs, grabBanner, doRdns, scanCts.Token);
                            if (res != null && res.Status == "OPEN")
                            {
                                Interlocked.Increment(ref openCount);
                                lock (scanResults) { scanResults.Add(res); }

                                BeginInvoke((Action)(() =>
                                {
                                    int rowIdx = gridResults.Rows.Add(
                                        res.Status, res.IP, res.Port, res.Service,
                                        res.LatencyMs.ToString("F1"), res.Hostname,
                                        res.HttpStatus + " " + res.HttpServer,
                                        res.HttpTitle
                                    );
                                    gridResults.Rows[rowIdx].Cells[0].Style.ForeColor = Color.FromArgb(6, 214, 160);
                                    gridResults.Rows[rowIdx].Cells[2].Style.ForeColor = Color.FromArgb(255, 209, 102);
                                }));
                            }

                            int c = Interlocked.Increment(ref completedTasks);
                            if (c % 5 == 0 || c == totalTasks)
                            {
                                BeginInvoke((Action)(() =>
                                {
                                    prgScan.Value = Math.Min(c, totalTasks);
                                    double elapsed = (DateTime.Now - startTime).TotalSeconds;
                                    double rate = elapsed > 0 ? c / elapsed : 0;
                                    lblProgressStats.Text = string.Format("Прогресс: {0}/{1} ({2}%) | Открыто: {3} | Скорость: {4:F1} t/s",
                                        c, totalTasks, (int)((double)c / totalTasks * 100), openCount, rate);
                                }));
                            }
                        }
                        finally
                        {
                            sem.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            isScanning = false;
            touhou.IsDancing = false;
            btnScanSingle.Enabled = true;
            btnStartMassScan.Enabled = loadedFileTargets.Count > 0;
            btnLoadFile.Enabled = true;
            btnStopScan.Enabled = false;

            double totalElapsed = (DateTime.Now - startTime).TotalSeconds;
            touhou.StatusText = string.Format("Готово! Проверено {0} задач за {1:F1}s. Найдено открытых портов: {2} ✨",
                completedTasks, totalElapsed, openCount);
        }

        private void StopScan()
        {
            if (scanCts != null) scanCts.Cancel();
            touhou.StatusText = "Сканирование остановлено пользователем ✨";
        }

        private async Task<ScanResultRow> ScanPortAsync(string ip, int port, int timeoutMs, bool grabBanner, bool doRdns, CancellationToken ct)
        {
            var res = new ScanResultRow
            {
                IP = ip,
                Port = port,
                Service = GetServiceName(port),
                Status = "CLOSED",
                Hostname = "-",
                HttpStatus = "-",
                HttpServer = "-",
                HttpTitle = "-",
                Banner = "-"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var client = new TcpClient())
            {
                try
                {
                    var connectTask = client.ConnectAsync(ip, port);
                    var delayTask = Task.Delay(timeoutMs, ct);

                    if (await Task.WhenAny(connectTask, delayTask) == connectTask && client.Connected)
                    {
                        sw.Stop();
                        res.Status = "OPEN";
                        res.LatencyMs = sw.Elapsed.TotalMilliseconds;

                        if (doRdns)
                        {
                            try
                            {
                                var entry = await Dns.GetHostEntryAsync(ip);
                                if (!string.IsNullOrEmpty(entry.HostName))
                                    res.Hostname = entry.HostName;
                            }
                            catch { }
                        }

                        if (grabBanner && client.Connected)
                        {
                            try
                            {
                                var stream = client.GetStream();
                                stream.ReadTimeout = 1200;
                                stream.WriteTimeout = 1200;

                                if (port == 80 || port == 443 || port == 8080 || port == 8443 || port == 8000 || port == 8888)
                                {
                                    string req = string.Format("HEAD / HTTP/1.1\r\nHost: {0}\r\nUser-Agent: Mozilla/5.0 (ServerScanner)\r\nConnection: close\r\n\r\n", ip);
                                    byte[] reqBytes = Encoding.ASCII.GetBytes(req);
                                    await stream.WriteAsync(reqBytes, 0, reqBytes.Length);

                                    byte[] buf = new byte[2048];
                                    int read = await stream.ReadAsync(buf, 0, buf.Length);
                                    if (read > 0)
                                    {
                                        string resp = Encoding.UTF8.GetString(buf, 0, read);
                                        var mStatus = Regex.Match(resp, @"HTTP/\d\.\d\s+(\d{3}\s+[^\r\n]*)");
                                        if (mStatus.Success) res.HttpStatus = mStatus.Groups[1].Value.Trim();

                                        var mServer = Regex.Match(resp, @"(?i)Server:\s*([^\r\n]+)");
                                        if (mServer.Success) res.HttpServer = mServer.Groups[1].Value.Trim();

                                        var mTitle = Regex.Match(resp, @"(?i)<title>(.*?)</title>");
                                        if (mTitle.Success) res.HttpTitle = mTitle.Groups[1].Value.Trim();
                                        else res.HttpTitle = res.HttpServer;
                                    }
                                }
                                else
                                {
                                    byte[] buf = new byte[1024];
                                    int read = await stream.ReadAsync(buf, 0, buf.Length);
                                    if (read > 0)
                                    {
                                        string b = Encoding.ASCII.GetString(buf, 0, read).Trim();
                                        res.Banner = b.Length > 50 ? b.Substring(0, 50) : b;
                                        res.HttpTitle = res.Banner;
                                    }
                                }
                            }
                            catch { }
                        }
                        return res;
                    }
                }
                catch { }
            }
            return null;
        }

        private List<int> ParsePorts(string input)
        {
            var ports = new List<int>();
            if (string.IsNullOrEmpty(input)) return ports;

            string[] parts = input.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                string s = p.Trim();
                if (s.Contains("-"))
                {
                    string[] r = s.Split('-');
                    int start, end;
                    if (r.Length == 2 && int.TryParse(r[0], out start) && int.TryParse(r[1], out end))
                    {
                        for (int i = start; i <= end && i <= 65535; i++)
                            if (i > 0) ports.Add(i);
                    }
                }
                else
                {
                    int val;
                    if (int.TryParse(s, out val) && val > 0 && val <= 65535)
                        ports.Add(val);
                }
            }
            return ports;
        }

        private List<string> ExpandCidr(string cidr)
        {
            var list = new List<string>();
            try
            {
                string[] parts = cidr.Split('/');
                if (parts.Length == 2 && parts[1] == "24")
                {
                    string[] octets = parts[0].Split('.');
                    if (octets.Length == 4)
                    {
                        string prefix = octets[0] + "." + octets[1] + "." + octets[2] + ".";
                        for (int i = 1; i <= 254; i++)
                            list.Add(prefix + i);
                        return list;
                    }
                }
            }
            catch { }
            list.Add(cidr);
            return list;
        }

        private string GetServiceName(int port)
        {
            switch (port)
            {
                case 21: return "FTP";
                case 22: return "SSH";
                case 23: return "Telnet";
                case 25: return "SMTP";
                case 53: return "DNS";
                case 80: return "HTTP";
                case 110: return "POP3";
                case 143: return "IMAP";
                case 443: return "HTTPS";
                case 445: return "SMB";
                case 1433: return "MSSQL";
                case 1521: return "Oracle";
                case 3306: return "MySQL";
                case 3389: return "RDP";
                case 5432: return "PostgreSQL";
                case 5900: return "VNC";
                case 6379: return "Redis";
                case 8080: return "HTTP-Proxy";
                case 8443: return "HTTPS-Alt";
                case 27017: return "MongoDB";
                default: return "TCP";
            }
        }

        private void ExportResults(string format)
        {
            if (scanResults.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта! Сначала выполните сканирование.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                if (format == "json") sfd.Filter = "JSON Files (*.json)|*.json";
                else if (format == "csv") sfd.Filter = "CSV Files (*.csv)|*.csv";
                else sfd.Filter = "Excel Files (*.xls)|*.xls";
                
                sfd.FileName = "results." + format;
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (format == "json")
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("[");
                            for (int i = 0; i < scanResults.Count; i++)
                            {
                                var r = scanResults[i];
                                sb.AppendFormat("  {{\n    \"ip\": \"{0}\",\n    \"port\": {1},\n    \"service\": \"{2}\",\n    \"status\": \"{3}\",\n    \"latency_ms\": {4:F1},\n    \"hostname\": \"{5}\",\n    \"http_status\": \"{6}\",\n    \"http_server\": \"{7}\",\n    \"http_title\": \"{8}\"\n  }}",
                                    r.IP, r.Port, r.Service, r.Status, r.LatencyMs, r.Hostname, r.HttpStatus, r.HttpServer, r.HttpTitle);
                                if (i < scanResults.Count - 1) sb.Append(",");
                                sb.AppendLine();
                            }
                            sb.AppendLine("]");
                            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        }
                        else if (format == "csv")
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("IP,Port,Service,Status,LatencyMs,Hostname,HttpStatus,HttpServer,HttpTitle");
                            foreach (var r in scanResults)
                            {
                                sb.AppendFormat("\"{0}\",{1},\"{2}\",\"{3}\",{4:F1},\"{5}\",\"{6}\",\"{7}\",\"{8}\"\n",
                                    r.IP, r.Port, r.Service, r.Status, r.LatencyMs, r.Hostname, r.HttpStatus, r.HttpServer, r.HttpTitle);
                            }
                            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        }
                        else
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("<html><head><meta charset='utf-8'></head><body><table>");
                            sb.AppendLine("<tr><th style='background-color:#ff3377;color:white;'>IP</th><th style='background-color:#ff3377;color:white;'>Port</th><th style='background-color:#ff3377;color:white;'>Service</th><th style='background-color:#ff3377;color:white;'>Status</th><th style='background-color:#ff3377;color:white;'>LatencyMs</th><th style='background-color:#ff3377;color:white;'>Hostname</th><th style='background-color:#ff3377;color:white;'>HttpStatus</th><th style='background-color:#ff3377;color:white;'>HttpServer</th><th style='background-color:#ff3377;color:white;'>HttpTitle</th></tr>");
                            foreach (var r in scanResults)
                            {
                                sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>OPEN</td><td>{3:F1}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td></tr>\n",
                                    r.IP, r.Port, r.Service, r.LatencyMs, r.Hostname, r.HttpStatus, r.HttpServer, r.HttpTitle);
                            }
                            sb.AppendLine("</table></body></html>");
                            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        }
                        MessageBox.Show("Файл успешно сохранён: " + sfd.FileName, "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка сохранения файла: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }

    // =========================================================================
    // 🚀 Application Entry Point
    // =========================================================================
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
