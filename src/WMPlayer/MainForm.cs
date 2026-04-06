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
    private bool _isCursorHidden;
    private int _lastVolumeBeforeMute = 80;
    private Point _lastMousePosition = Point.Empty;
    private DateTime _lastInteractionAt = DateTime.MinValue;
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
        _player.Volume = _lastVolumeBeforeMute;

        _videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            MediaPlayer = _player,
            BackColor = Color.Black
        };
        _videoView.DoubleClick += (_, _) => ToggleFullScreen();

        _topBar = BuildTopBar();
        _bottomBar = BuildBottomBar();

        Controls.Add(_videoView);
        Controls.Add(_bottomBar);
        Controls.Add(_topBar);

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(140, 0, 0, 0),
            Padding = new Padding(8, 6, 8, 6),
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
        MouseMove += (_, e) => HandleInteraction(e.Location);
        _videoView.MouseMove += (_, e) => HandleInteraction(e.Location);
        _topBar.MouseMove += (_, e) => HandleInteraction(e.Location);
        _bottomBar.MouseMove += (_, e) => HandleInteraction(e.Location);

        foreach (Control control in _topBar.Controls)
        {
            control.MouseMove += (_, e) => HandleInteraction(e.Location);
        }

        foreach (Control control in _bottomBar.Controls)
        {
            control.MouseMove += (_, e) => HandleInteraction(e.Location);
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
            Height = 50,
            BackColor = Color.FromArgb(220, 22, 22, 22),
            Padding = new Padding(8, 10, 8, 8),
            WrapContents = false,
            AutoScroll = true
        };

        panel.Controls.Add(MakeButton("Abrir", (_, _) => OpenFileDialog()));
        panel.Controls.Add(MakeButton("⏯ Play/Pause", (_, _) => TogglePlayPause()));
        panel.Controls.Add(MakeButton("⏪ -5s", (_, _) => SeekRelative(-5000)));
        panel.Controls.Add(MakeButton("⏩ +5s", (_, _) => SeekRelative(5000)));
        panel.Controls.Add(MakeButton("⏮ Início", (_, _) => SeekToStart()));
        panel.Controls.Add(MakeButton("⏭ Fim", (_, _) => SeekToEnd()));
        panel.Controls.Add(MakeButton("⛶ Tela cheia", (_, _) => ToggleFullScreen()));

        return panel;
    }

    private Control BuildBottomBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            BackColor = Color.FromArgb(220, 22, 22, 22),
            Padding = new Padding(12)
        };

        var timeline = new TrackBar
        {
            Dock = DockStyle.Fill,
            TickStyle = TickStyle.None,
            Minimum = 0,
            Maximum = 1000,
            BackColor = Color.FromArgb(220, 22, 22, 22)
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
            BackColor = Color.FromArgb(38, 38, 38),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4, 0, 4, 0),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
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
            ShowMouseCursor();
            return;
        }

        _previousBorderStyle = FormBorderStyle;
        _previousBounds = Bounds;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        _isFullScreen = true;
        ShowMouseCursor();
        ShowControlsTemporarily();
    }

    private void HandleInteraction(Point location, bool force = false)
    {
        if (!_isFullScreen) return;
        var movedEnough = force || Math.Abs(location.X - _lastMousePosition.X) >= 4 || Math.Abs(location.Y - _lastMousePosition.Y) >= 4;
        var enoughTime = force || (DateTime.UtcNow - _lastInteractionAt).TotalMilliseconds >= 120;
        if (!movedEnough || !enoughTime) return;

        _lastMousePosition = location;
        _lastInteractionAt = DateTime.UtcNow;
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
        HideMouseCursor();
    }

    private void SetControlsVisibility(bool visible)
    {
        _topBar.Visible = visible;
        _bottomBar.Visible = visible;
        _statusLabel.Visible = !_isFullScreen || visible;

        if (visible)
        {
            ShowMouseCursor();
        }
    }

    private void HideMouseCursor()
    {
        if (_isCursorHidden) return;
        Cursor.Hide();
        _isCursorHidden = true;
    }

    private void ShowMouseCursor()
    {
        if (!_isCursorHidden) return;
        Cursor.Show();
        _isCursorHidden = false;
    }

    private void HandleKeyboardShortcuts(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            TogglePlayPause();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.K)
        {
            TogglePlayPause();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            SeekRelative(5000);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.L)
        {
            SeekRelative(10000);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Left)
        {
            SeekRelative(-5000);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.J)
        {
            SeekRelative(-10000);
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
        else if (e.KeyCode == Keys.F)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape && _isFullScreen)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            SetVolume(_player.Volume + 5);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            SetVolume(_player.Volume - 5);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.M)
        {
            ToggleMute();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.O)
        {
            OpenFileDialog();
            e.Handled = true;
        }

        if (e.Handled)
        {
            HandleInteraction(PointToClient(MousePosition), force: true);
        }
    }

    private void SetVolume(int volume)
    {
        var clamped = Math.Clamp(volume, 0, 125);
        _player.Volume = clamped;
        if (clamped > 0)
        {
            _lastVolumeBeforeMute = clamped;
        }
        UpdateStatus($"Volume: {clamped}%");
    }

    private void ToggleMute()
    {
        if (_player.Volume > 0)
        {
            _lastVolumeBeforeMute = _player.Volume;
            _player.Volume = 0;
            UpdateStatus("Mudo");
            return;
        }

        var restoreVolume = _lastVolumeBeforeMute <= 0 ? 80 : _lastVolumeBeforeMute;
        _player.Volume = restoreVolume;
        UpdateStatus($"Volume: {restoreVolume}%");
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
            ShowMouseCursor();
            _player.Dispose();
            _libVlc.Dispose();
        }

        base.Dispose(disposing);
    }
}
