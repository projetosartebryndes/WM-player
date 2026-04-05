using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace WMPlayer;

public sealed class MainForm : Form
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly VideoView _videoView;
    private readonly TrackBar _timeline;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _uiTimer;
    private readonly System.Windows.Forms.Timer _fullScreenControlsTimer;
    private readonly Control _topBar;
    private readonly Control _bottomBar;

    private string? _currentMediaPath;
    private bool _isDraggingTimeline;
    private bool _isFullScreen;
    private FormBorderStyle _previousBorderStyle;
    private Rectangle _previousBounds;
    private readonly PlaybackStateStore _stateStore = new();

    public MainForm(string? launchMediaPath)
    {
        Core.Initialize();

        Text = "WM-player";
        Width = 1280;
        Height = 760;
        MinimumSize = new Size(800, 500);
        KeyPreview = true;

        _libVlc = new LibVLC("--avcodec-hw=d3d11va", "--file-caching=1000", "--network-caching=1000");
        _player = new MediaPlayer(_libVlc);

        _videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            MediaPlayer = _player,
            BackColor = Color.Black
        };

        _topBar = BuildTopBar();
        _bottomBar = BuildBottomBar();

        Controls.Add(_videoView);
        Controls.Add(_bottomBar);
        Controls.Add(_topBar);

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(110, 0, 0, 0),
            Padding = new Padding(6),
            Location = new Point(10, 50),
            Text = "Nenhum vídeo aberto"
        };

        _timeline = (TrackBar)_bottomBar.Controls[0];
        Controls.Add(_statusLabel);
        _statusLabel.BringToFront();

        _player.EndReached += (_, _) => BeginInvoke(() => UpdateStatus("Reprodução finalizada"));
        _player.Playing += (_, _) => BeginInvoke(() => UpdateStatus("Reproduzindo"));
        _player.Paused += (_, _) => BeginInvoke(() => UpdateStatus("Pausado"));

        _uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _uiTimer.Tick += (_, _) => UpdateTimeline();
        _uiTimer.Start();

        _fullScreenControlsTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _fullScreenControlsTimer.Tick += (_, _) => HideControlsInFullScreen();

        FormClosing += (_, _) => PersistCurrentPlaybackPosition();
        KeyDown += HandleKeyboardShortcuts;
        MouseMove += (_, _) => HandleInteraction();
        _videoView.MouseMove += (_, _) => HandleInteraction();
        _topBar.MouseMove += (_, _) => HandleInteraction();
        _bottomBar.MouseMove += (_, _) => HandleInteraction();

        foreach (Control control in _topBar.Controls)
        {
            control.MouseMove += (_, _) => HandleInteraction();
        }

        foreach (Control control in _bottomBar.Controls)
        {
            control.MouseMove += (_, _) => HandleInteraction();
        }

        if (!string.IsNullOrWhiteSpace(launchMediaPath) && File.Exists(launchMediaPath))
        {
            OpenMedia(launchMediaPath);
        }
    }

    private Control BuildTopBar()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(6),
            WrapContents = false,
            AutoScroll = true
        };

        panel.Controls.Add(MakeButton("Abrir", (_, _) => OpenFileDialog()));
        panel.Controls.Add(MakeButton("Play/Pause", (_, _) => TogglePlayPause()));
        panel.Controls.Add(MakeButton("-5s", (_, _) => SeekRelative(-5000)));
        panel.Controls.Add(MakeButton("+5s", (_, _) => SeekRelative(5000)));
        panel.Controls.Add(MakeButton("Início", (_, _) => SeekToStart()));
        panel.Controls.Add(MakeButton("Fim", (_, _) => SeekToEnd()));
        panel.Controls.Add(MakeButton("Tela cheia", (_, _) => ToggleFullScreen()));

        return panel;
    }

    private Control BuildBottomBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(12)
        };

        var timeline = new TrackBar
        {
            Dock = DockStyle.Fill,
            TickStyle = TickStyle.None,
            Minimum = 0,
            Maximum = 1000
        };

        timeline.MouseDown += (_, _) => _isDraggingTimeline = true;
        timeline.MouseUp += (_, _) =>
        {
            _isDraggingTimeline = false;
            if (_player.Length > 0)
            {
                _player.Time = (long)((timeline.Value / (double)timeline.Maximum) * _player.Length);
            }
        };

        panel.Controls.Add(timeline);
        return panel;
    }

    private Button MakeButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            Width = 110,
            Height = 28,
            BackColor = Color.FromArgb(52, 52, 52),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4, 0, 4, 0)
        };

        button.FlatAppearance.BorderColor = Color.DimGray;
        button.Click += click;
        return button;
    }

    private void OpenFileDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Abrir vídeo",
            Filter = "Vídeos|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.m4v;*.webm;*.ts|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            OpenMedia(dialog.FileName);
        }
    }

    private void OpenMedia(string mediaPath)
    {
        PersistCurrentPlaybackPosition();

        using var media = new Media(_libVlc, mediaPath, FromType.FromPath);
        _player.Play(media);
        _currentMediaPath = mediaPath;
        Text = $"WM-player - {Path.GetFileName(mediaPath)}";

        var resumeAt = _stateStore.GetResumePosition(mediaPath);
        if (resumeAt > 0)
        {
            Task.Delay(600).ContinueWith(_ => BeginInvoke(() =>
            {
                if (_player.IsPlaying && _player.Length > resumeAt)
                {
                    _player.Time = resumeAt;
                    UpdateStatus($"Retomado em {TimeSpan.FromMilliseconds(resumeAt):hh\\:mm\\:ss}");
                }
            }));
        }
    }

    private void TogglePlayPause()
    {
        if (_player.IsPlaying)
        {
            _player.Pause();
            PersistCurrentPlaybackPosition();
        }
        else if (_player.Media is not null)
        {
            _player.Play();
        }
        else
        {
            OpenFileDialog();
        }
    }

    private void SeekRelative(long deltaMs)
    {
        if (_player.Length <= 0) return;
        var target = Math.Clamp(_player.Time + deltaMs, 0, _player.Length);
        _player.Time = target;
        UpdateStatus(deltaMs > 0 ? "Avançou 5 segundos" : "Retrocedeu 5 segundos");
    }

    private void SeekToStart()
    {
        if (_player.Length <= 0) return;
        _player.Time = 0;
        UpdateStatus("Voltou para o início");
    }

    private void SeekToEnd()
    {
        if (_player.Length <= 0) return;
        _player.Time = Math.Max(_player.Length - 750, 0);
        UpdateStatus("Pulou para o fim");
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            FormBorderStyle = _previousBorderStyle;
            Bounds = _previousBounds;
            WindowState = FormWindowState.Normal;
            _isFullScreen = false;
            SetControlsVisibility(true);
            _fullScreenControlsTimer.Stop();
            return;
        }

        _previousBorderStyle = FormBorderStyle;
        _previousBounds = Bounds;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        _isFullScreen = true;
        ShowControlsTemporarily();
    }

    private void HandleInteraction()
    {
        if (!_isFullScreen) return;
        ShowControlsTemporarily();
    }

    private void ShowControlsTemporarily()
    {
        SetControlsVisibility(true);
        _fullScreenControlsTimer.Stop();
        _fullScreenControlsTimer.Start();
    }

    private void HideControlsInFullScreen()
    {
        _fullScreenControlsTimer.Stop();
        if (!_isFullScreen) return;
        SetControlsVisibility(false);
    }

    private void SetControlsVisibility(bool visible)
    {
        _topBar.Visible = visible;
        _bottomBar.Visible = visible;
    }

    private void HandleKeyboardShortcuts(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            TogglePlayPause();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            SeekRelative(5000);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Left)
        {
            SeekRelative(-5000);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Home)
        {
            SeekToStart();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.End)
        {
            SeekToEnd();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.O)
        {
            OpenFileDialog();
            e.Handled = true;
        }
    }

    private void UpdateTimeline()
    {
        if (_isDraggingTimeline || _player.Length <= 0) return;
        var progress = _player.Time / (double)_player.Length;
        _timeline.Value = (int)Math.Clamp(progress * _timeline.Maximum, _timeline.Minimum, _timeline.Maximum);
    }

    private void PersistCurrentPlaybackPosition()
    {
        if (string.IsNullOrWhiteSpace(_currentMediaPath) || _player.Length <= 0) return;
        _stateStore.SetResumePosition(_currentMediaPath, _player.Time, _player.Length);
    }

    private void UpdateStatus(string status)
    {
        var elapsed = _player.Time > 0 ? TimeSpan.FromMilliseconds(_player.Time).ToString("hh\\:mm\\:ss") : "00:00:00";
        var total = _player.Length > 0 ? TimeSpan.FromMilliseconds(_player.Length).ToString("hh\\:mm\\:ss") : "--:--:--";
        _statusLabel.Text = $"{status} | {elapsed} / {total}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _uiTimer.Dispose();
            _fullScreenControlsTimer.Dispose();
            _player.Dispose();
            _libVlc.Dispose();
        }

        base.Dispose(disposing);
    }
}
