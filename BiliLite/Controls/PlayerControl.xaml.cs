﻿using BiliLite.Helpers;
using BiliLite.Modules;
using FFmpegInterop;
using NSDanmaku.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using Windows.System.Display;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Graphics.Display;
using System.Text.RegularExpressions;
using Windows.UI.Core;


//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace BiliLite.Controls
{

    public enum VideoPlayType
    {
        /// <summary>
        /// 视频
        /// </summary>
        Video,
        /// <summary>
        /// 番剧、影视
        /// </summary>
        Season,
        /// <summary>
        /// 本地视频
        /// </summary>
        LocalVideo,
        /// <summary>
        /// 本地番剧、影视
        /// </summary>
        LoaclSeason
    }

    public class PlayInfo
    {
        /// <summary>
        /// 播放模式
        /// </summary>
        public VideoPlayType play_mode { get; set; }
        /// <summary>
        /// 排序
        /// </summary>
        public int order { get; set; }
        /// <summary>
        /// 专题ID
        /// </summary>
        public int season_id { get; set; }
        /// <summary>
        /// 专题类型
        /// </summary>
        public int season_type { get; set; }
        /// <summary>
        /// 专题分集ID
        /// </summary>
        public string ep_id { get; set; }
        /// <summary>
        /// 视频ID
        /// </summary>
        public string avid { get; set; }
        /// <summary>
        /// 必须，视频分集ID
        /// </summary>
        public string cid { get; set; }
        /// <summary>
        /// 标题
        /// </summary>
        public string title { get; set; }
        /// <summary>
        /// 是否VIP
        /// </summary>
        public bool is_vip { get; set; }
        /// <summary>
        /// 是否互动视频
        /// </summary>
        public bool is_interaction { get; set; } = false;
        /// <summary>
        /// 互动视频分支ID
        /// </summary>
        public int node_id { get; set; } = 0;
        /// <summary>
        /// 时长（毫秒）
        /// </summary>
        public int duration { get; set; }
        /// <summary>
        /// 视频信息
        /// </summary>
        public PlayUrlInfo play_url_info { get; set; }
        public object parameter { get; set; }
    }


    public sealed partial class PlayerControl : UserControl, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void DoPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 铺满窗口事件
        /// </summary>
        public event EventHandler<bool> FullWindowEvent;
        /// <summary>
        /// 全屏事件
        /// </summary>
        public event EventHandler<bool> FullScreenEvent;
        /// <summary>
        /// 切换剧集事件
        /// </summary>

        public event EventHandler<int> ChangeEpisodeEvent;
        /// <summary>
        /// 播放列表
        /// </summary>
        public List<PlayInfo> PlayInfos { get; set; }
        /// <summary>
        /// 当前播放
        /// </summary>
        public int CurrentPlayIndex { get; set; }
        /// <summary>
        /// 当前播放
        /// </summary>
        public PlayInfo CurrentPlayItem { get; set; }
        readonly PlayerVM playerHelper;
        readonly NSDanmaku.Helper.DanmakuParse danmakuParse;
        private PlayUrlReturnInfo _playUrlInfo;
        /// <summary>
        /// 播放地址信息
        /// </summary>
        public PlayUrlReturnInfo playUrlInfo
        {
            get { return _playUrlInfo; }
            set { _playUrlInfo = value; DoPropertyChanged("playUrlInfo"); }
        }

        DispatcherTimer danmuTimer;
        /// <summary>
        /// 弹幕信息
        /// </summary>
        List<NSDanmaku.Model.DanmakuModel> danmakuPool;
        SettingVM settingVM;
        DisplayRequest dispRequest;
        SystemMediaTransportControls _systemMediaTransportControls;
        DispatcherTimer timer_focus;
        public PlayerControl()
        {
            this.InitializeComponent();
            dispRequest = new DisplayRequest();
            playerHelper = new PlayerVM();
            settingVM = new SettingVM();
            danmakuParse = new NSDanmaku.Helper.DanmakuParse();
            //没过一秒就设置焦点
            timer_focus = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
            timer_focus.Tick += Timer_focus_Tick;
            danmuTimer = new DispatcherTimer();
            danmuTimer.Interval = TimeSpan.FromSeconds(1);
            danmuTimer.Tick += DanmuTimer_Tick;
            this.Loaded += PlayerControl_Loaded;
            this.Unloaded += PlayerControl_Unloaded;
        }

        private void Timer_focus_Tick(object sender, object e)
        {
            var elent = FocusManager.GetFocusedElement();
            if (elent is Button|| elent is AppBarButton|| elent is HyperlinkButton)
            {
                BtnFoucs.Focus(FocusState.Programmatic);
            }
           
        }

        bool runing = false;
        bool pointer_in_player = false;
        private async void ShowControl(bool show)
        {
            if (runing) return;
            runing = true;
            if (show)
            {
                
                control.Visibility = Visibility.Visible;
                await control.FadeInAsync(400);
            }
            else
            {
                if (pointer_in_player)
                {
                    Window.Current.CoreWindow.PointerCursor = null;
                }
                await control.FadeOutAsync(400);
                control.Visibility = Visibility.Collapsed;
            }
            runing = false;
        }
        private void PlayerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            Window.Current.CoreWindow.KeyDown -= PlayerControl_KeyDown;
            if (_systemMediaTransportControls != null)
            {
                _systemMediaTransportControls.DisplayUpdater.ClearAll();
                _systemMediaTransportControls.IsEnabled = false;
                _systemMediaTransportControls = null;
            }
            timer_focus.Stop();
        }
        private void PlayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            DanmuControl.ClearAll();
            Window.Current.CoreWindow.KeyDown += PlayerControl_KeyDown;
            BtnFoucs.Focus(FocusState.Programmatic);
            _systemMediaTransportControls = SystemMediaTransportControls.GetForCurrentView();
            _systemMediaTransportControls.IsPlayEnabled = true;
            _systemMediaTransportControls.IsPauseEnabled = true;
            if (CurrentPlayItem != null)
            {
                SystemMediaTransportControlsDisplayUpdater updater = _systemMediaTransportControls.DisplayUpdater;
                updater.Type = MediaPlaybackType.Video;
                updater.VideoProperties.Title = CurrentPlayItem.title;
                updater.Update();
            }
            _systemMediaTransportControls.ButtonPressed += _systemMediaTransportControls_ButtonPressed;
            LoadPlayerSetting();
            LoadDanmuSetting();
            LoadSutitleSetting();
            danmuTimer.Start();
            timer_focus.Start();
        }

        private async void _systemMediaTransportControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        Player.Play();
                    });
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        Player.Pause();
                    });
                    break;
                default:
                    break;
            }
        }

        private async void PlayerControl_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            var elent = FocusManager.GetFocusedElement();
            if (elent is TextBox|| elent is AutoSuggestBox)
            {
                args.Handled = false;
                return;
            }
            args.Handled = true;
            switch (args.VirtualKey)
            {
                case Windows.System.VirtualKey.Space:
                    if (Player.PlayState == PlayState.Playing || Player.PlayState == PlayState.End)
                    {
                        Player.Pause();
                    }
                    else
                    {
                        Player.Play();
                    }
                    break;
                case Windows.System.VirtualKey.Left:
                    {
                        if (Player.PlayState == PlayState.Playing || Player.PlayState == PlayState.Pause)
                        {
                            var _position = Player.Position - 3;
                            if (_position < 0)
                            {
                                _position = 0;
                            }
                            Player.Position = _position;
                            TxtToolTip.Text ="进度:"+TimeSpan.FromSeconds(Player.Position).ToString(@"hh\:mm\:ss");
                            ToolTip.Visibility = Visibility.Visible;
                            await Task.Delay(2000);
                            ToolTip.Visibility = Visibility.Collapsed;
                        }
                    }

                    break;
                case Windows.System.VirtualKey.Right:
                    {
                        if (Player.PlayState == PlayState.Playing || Player.PlayState == PlayState.Pause)
                        {
                            var _position = Player.Position + 3;
                            if (_position > Player.Duration)
                            {
                                _position = Player.Duration;
                            }
                            Player.Position = _position;
                            TxtToolTip.Text = "进度:" + TimeSpan.FromSeconds(Player.Position).ToString(@"hh\:mm\:ss");
                            ToolTip.Visibility = Visibility.Visible;
                            await Task.Delay(2000);
                            ToolTip.Visibility = Visibility.Collapsed;
                        }
                    }
                    break;
                case Windows.System.VirtualKey.Up:
                    Player.Volume += 0.1;
                    TxtToolTip.Text = "音量:" + Player.Volume.ToString("P");
                    ToolTip.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;

                case Windows.System.VirtualKey.Down:
                    Player.Volume -= 0.1;
                    if (Player.Volume==0)
                    {
                        TxtToolTip.Text = "静音";
                    }
                    else
                    {
                        TxtToolTip.Text = "音量:" + Player.Volume.ToString("P");
                    }
                    ToolTip.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;
                case Windows.System.VirtualKey.Escape:
                    IsFullScreen = false;
                    break;
                case Windows.System.VirtualKey.F12:
                case Windows.System.VirtualKey.W:
                    IsFullWindow = !IsFullWindow;
                    break;
                case Windows.System.VirtualKey.F11:
                case Windows.System.VirtualKey.F:
                case Windows.System.VirtualKey.Enter:
                    IsFullScreen =!IsFullScreen;
                    break;
                case Windows.System.VirtualKey.F10:
                    await CaptureVideo();
                    break;
                case Windows.System.VirtualKey.O:
                case Windows.System.VirtualKey.P:
                    {
                        if (Player.PlayState == PlayState.Playing || Player.PlayState == PlayState.Pause)
                        {
                            var _position = Player.Position + 90;
                            if (_position > Player.Duration)
                            {
                                _position = Player.Duration;
                            }
                            Player.Position = _position;
                            TxtToolTip.Text = "跳过OP(快进90秒)";
                            ToolTip.Visibility = Visibility.Visible;
                            await Task.Delay(2000);
                            ToolTip.Visibility = Visibility.Collapsed;
                        }
                    }
                    break;
                case Windows.System.VirtualKey.F9:
                case Windows.System.VirtualKey.D:
                    if (DanmuControl.Visibility == Visibility.Visible)
                    {
                        DanmuControl.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        DanmuControl.Visibility = Visibility.Visible;
                    }
                    break;
                case Windows.System.VirtualKey.Z:
                case Windows.System.VirtualKey.N:
                    if (EpisodeList.SelectedIndex==0)
                    {
                        Utils.ShowMessageToast("已经是第一P了");
                    }
                    else
                    {
                        await SetPlayItem(EpisodeList.SelectedIndex - 1);
                    }
                    break;
                case Windows.System.VirtualKey.X:
                case Windows.System.VirtualKey.M:
                    if (EpisodeList.SelectedIndex == EpisodeList.Items.Count-1)
                    {
                        Utils.ShowMessageToast("已经是最后一P了");
                    }
                    else
                    {
                        await SetPlayItem(EpisodeList.SelectedIndex + 1);
                    }
                    break;
                default:
                    break;
            }
        }


        private void LoadDanmuSetting()
        {
            //顶部
            DanmuSettingHideTop.IsOn = SettingHelper.GetValue<bool>(SettingHelper.VideoDanmaku.HIDE_TOP, false);
            if (DanmuSettingHideTop.IsOn)
            {
                DanmuControl.HideDanmaku(DanmakuLocation.Top);
            }
            DanmuSettingHideTop.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.VideoDanmaku.HIDE_TOP, DanmuSettingHideTop.IsOn);
                if (DanmuSettingHideTop.IsOn)
                {
                    DanmuControl.HideDanmaku(DanmakuLocation.Top);
                }
                else
                {
                    DanmuControl.ShowDanmaku(DanmakuLocation.Top);
                }
            });
            //底部
            DanmuSettingHideBottom.IsOn = SettingHelper.GetValue<bool>(SettingHelper.VideoDanmaku.HIDE_BOTTOM, false);
            if (DanmuSettingHideBottom.IsOn)
            {
                DanmuControl.HideDanmaku(DanmakuLocation.Bottom);
            }
            DanmuSettingHideBottom.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.VideoDanmaku.HIDE_BOTTOM, DanmuSettingHideBottom.IsOn);
                if (DanmuSettingHideBottom.IsOn)
                {
                    DanmuControl.HideDanmaku(DanmakuLocation.Bottom);
                }
                else
                {
                    DanmuControl.ShowDanmaku(DanmakuLocation.Bottom);
                }
            });
            //滚动
            DanmuSettingHideRoll.IsOn = SettingHelper.GetValue<bool>(SettingHelper.VideoDanmaku.HIDE_ROLL, false);
            if (DanmuSettingHideRoll.IsOn)
            {
                DanmuControl.HideDanmaku(DanmakuLocation.Roll);
            }
            DanmuSettingHideRoll.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.VideoDanmaku.HIDE_ROLL, DanmuSettingHideRoll.IsOn);
                if (DanmuSettingHideRoll.IsOn)
                {
                    DanmuControl.HideDanmaku(DanmakuLocation.Roll);
                }
                else
                {
                    DanmuControl.ShowDanmaku(DanmakuLocation.Roll);
                }
            });
            //弹幕大小
            DanmuControl.sizeZoom = SettingHelper.GetValue<double>(SettingHelper.VideoDanmaku.FONT_ZOOM, 1);
            DanmuSettingFontZoom.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.VideoDanmaku.FONT_ZOOM, DanmuSettingFontZoom.Value);
            });
            //弹幕速度
            DanmuControl.speed = SettingHelper.GetValue<int>(SettingHelper.VideoDanmaku.SPEED, 10);
            DanmuSettingSpeed.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.VideoDanmaku.SPEED, DanmuSettingSpeed.Value);
            });
            //弹幕透明度
            DanmuControl.Opacity = SettingHelper.GetValue<double>(SettingHelper.VideoDanmaku.OPACITY, 1.0);
            DanmuSettingOpacity.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.VideoDanmaku.OPACITY, DanmuSettingOpacity.Value);
            });
            //弹幕加粗
            DanmuControl.bold = SettingHelper.GetValue<bool>(SettingHelper.VideoDanmaku.BOLD, false);
            DanmuSettingBold.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.VideoDanmaku.BOLD, DanmuSettingBold.IsOn);
            });
            //弹幕样式
            DanmuControl.BorderStyle = SettingHelper.GetValue<int>(SettingHelper.VideoDanmaku.BORDER_STYLE, 2);
            DanmuSettingStyle.SelectionChanged += new SelectionChangedEventHandler((e, args) =>
            {
                if (DanmuSettingStyle.SelectedIndex != -1)
                {
                    SettingHelper.SetValue<int>(SettingHelper.VideoDanmaku.BORDER_STYLE, DanmuSettingStyle.SelectedIndex);
                }
            });
            //合并弹幕
            DanmuSettingMerge.IsOn = SettingHelper.GetValue<bool>(SettingHelper.VideoDanmaku.MERGE, false);
            DanmuSettingMerge.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.VideoDanmaku.MERGE, DanmuSettingMerge.IsOn);
            });
            //半屏显示
            DanmuSettingDotHideSubtitle.IsOn = SettingHelper.GetValue<bool>(SettingHelper.VideoDanmaku.DOTNET_HIDE_SUBTITLE, false);
            DanmuSettingDotHideSubtitle.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.VideoDanmaku.DOTNET_HIDE_SUBTITLE, DanmuSettingDotHideSubtitle.IsOn);
            });

            //弹幕开关
            DanmuControl.Visibility = SettingHelper.GetValue<Visibility>(SettingHelper.VideoDanmaku.SHOW, Visibility.Visible);
            DanmuSettingWords.ItemsSource = settingVM.ShieldWords;
        }
        private void LoadPlayerSetting()
        {
            //音量
            Player.Volume = SettingHelper.GetValue<double>(SettingHelper.Player.VOLUME, 1.0);
            SliderVolume.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.Player.VOLUME, SliderVolume.Value);
            });
            //播放模式
            PlayerSettingMode.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.Player.DEFAULT_VIDEO_TYPE, 1);
            PlayerSettingMode.SelectionChanged += new SelectionChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<int>(SettingHelper.Player.DEFAULT_VIDEO_TYPE, PlayerSettingMode.SelectedIndex);
            });
            //播放列表
            PlayerSettingPlayMode.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.Player.DEFAULT_PLAY_MODE, 0);
            PlayerSettingPlayMode.SelectionChanged += new SelectionChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<int>(SettingHelper.Player.DEFAULT_PLAY_MODE, PlayerSettingPlayMode.SelectedIndex);
            });
            //使用其他网站视频
            PlayerSettingUseOtherSite.IsOn = SettingHelper.GetValue<bool>(SettingHelper.Player.USE_OTHER_SITEVIDEO, true);
            PlayerSettingUseOtherSite.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.Player.USE_OTHER_SITEVIDEO, PlayerSettingUseOtherSite.IsOn);
            });
            //播放比例
            PlayerSettingRatio.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.Player.RATIO, 0);
            Player.SetRatioMode(PlayerSettingRatio.SelectedIndex);
            PlayerSettingRatio.SelectionChanged += new SelectionChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<int>(SettingHelper.Player.RATIO, PlayerSettingRatio.SelectedIndex);
                Player.SetRatioMode(PlayerSettingRatio.SelectedIndex);
            });
            _autoPlay = SettingHelper.GetValue<bool>(SettingHelper.Player.AUTO_PLAY, false);
        }
        private void LoadSutitleSetting()
        {
            //字幕大小
            SubtitleSettingSize.Value = SettingHelper.GetValue<double>(SettingHelper.Player.SUBTITLE_SIZE, 25);
            SubtitleSettingSize.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.Player.SUBTITLE_SIZE, SubtitleSettingSize.Value);
            });
            //字幕颜色
            SubtitleSettingColor.SelectedIndex = SettingHelper.GetValue<int>(SettingHelper.Player.SUBTITLE_COLOR, 0);
            TxtSubtitle.Foreground = new SolidColorBrush(Utils.ToColor((SubtitleSettingColor.SelectedItem as ComboBoxItem).Tag.ToString()));
            SubtitleSettingColor.SelectionChanged += new SelectionChangedEventHandler((e, args) =>
            {
                TxtSubtitle.Foreground = new SolidColorBrush(Utils.ToColor((SubtitleSettingColor.SelectedItem as ComboBoxItem).Tag.ToString()));
                SettingHelper.SetValue<int>(SettingHelper.Player.SUBTITLE_COLOR, SubtitleSettingColor.SelectedIndex);
            });
            //字幕透明度
            SubtitleSettingOpacity.Value = SettingHelper.GetValue<double>(SettingHelper.Player.SUBTITLE_OPACITY, 1.0);
            SubtitleSettingOpacity.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.Player.SUBTITLE_OPACITY, SubtitleSettingOpacity.Value);
            });
            //字幕底部距离
            SubtitleSettingBottom.Value = SettingHelper.GetValue<double>(SettingHelper.Player.SUBTITLE_BOTTOM, 24);
            BorderSubtitle.Margin = new Thickness(0, 0, 0, SubtitleSettingBottom.Value);
            SubtitleSettingBottom.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                BorderSubtitle.Margin = new Thickness(0, 0, 0, SubtitleSettingBottom.Value);
                SettingHelper.SetValue<double>(SettingHelper.Player.SUBTITLE_BOTTOM, SubtitleSettingBottom.Value);
            });
        }

        public void InitializePlayInfo(List<PlayInfo> playInfos, int index)
        {
            //保持屏幕常亮
            dispRequest.RequestActive();
            PlayInfos = playInfos;
            EpisodeList.ItemsSource = PlayInfos;
            if (PlayInfos.Count > 1)
            {
                ShowPlaylistButton = true;
            }
            else if (PlayInfos.Count == 1 && PlayInfos[0].is_interaction)
            {
                ShowPlaylistButton = true;
            }
            else
            {
                ShowPlaylistButton = false;
            }

            EpisodeList.SelectedIndex = index;



        }

        private async void DanmuTimer_Tick(object sender, object e)
        {
            if (showControlsFlag != -1)
            {
                if (showControlsFlag >= 5)
                {
                    var elent = FocusManager.GetFocusedElement();
                    if (!(elent is TextBox) && !(elent is AutoSuggestBox))
                    {
                        ShowControl(false);
                        showControlsFlag = -1;
                    }
                    //FadeOut.Begin();
                    //control.Visibility = Visibility.Collapsed;
                 
                }
                else
                {
                    showControlsFlag++;
                }
            }
            if (Buffering)
            {
                return;
            }
            if (Player.PlayState != PlayState.Playing)
            {
                return;
            }
            if (DanmuControl.Visibility == Visibility.Collapsed)
            {
                return;
            }
            var needDistinct = DanmuSettingMerge.IsOn;
            var p = Convert.ToInt32(Player.Position);
            await Task.Run(async () =>
            {
                try
                {
                    if (danmakuPool != null)
                    {
                        var data = danmakuPool.Where(x => x.time_s == p);
                        //去重
                        if (needDistinct)
                        {
                            data = data.Distinct(new CompareDanmakuModel());
                        }
                        //关键词
                        foreach (var item in settingVM.ShieldWords)
                        {
                            data = data.Where(x => !x.text.Contains(item));
                        }
                        //用户
                        foreach (var item in settingVM.ShieldUsers)
                        {
                            data = data.Where(x => !x.sendID.Equals(item));
                        }
                        //正则
                        foreach (var item in settingVM.ShieldRegulars)
                        {
                            data = data.Where(x => !Regex.IsMatch(x.text, item));
                        }

                        //加载弹幕
                        foreach (var item in data)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
                            {
                                DanmuControl.AddDanmu(item, false);
                            });
                        }
                    }
                }
                catch (Exception)
                {
                }
            });

        }

        private async Task SetPlayItem(int index)
        {
            if (PlayInfos == null || PlayInfos.Count == 0)
            {
                return;
            }

            Player.ClosePlay();
            if (index >= PlayInfos.Count)
            {
                index = PlayInfos.Count - 1;
            }
            CurrentPlayIndex = index;
            CurrentPlayItem = PlayInfos[index];
            //设置标题
            TopTitle.Text = CurrentPlayItem.title;
            if (_systemMediaTransportControls != null)
            {
                SystemMediaTransportControlsDisplayUpdater updater = _systemMediaTransportControls.DisplayUpdater;
                updater.Type = MediaPlaybackType.Video;
                updater.VideoProperties.Title = CurrentPlayItem.title;
                updater.Update();
            }

            //设置下一集按钮的显示
            if (PlayInfos.Count >= 1 && index != PlayInfos.Count - 1)
            {
                BottomBtnNext.Visibility = Visibility.Visible;
            }
            else
            {
                BottomBtnNext.Visibility = Visibility.Collapsed;
            }
            ChangeEpisodeEvent?.Invoke(this, index);

            playUrlInfo = null;
            if (CurrentPlayItem.play_mode == VideoPlayType.Season)
            {
                Player._ffmpegConfig.FFmpegOptions["referer"] = "https://www.bilibili.com/bangumi/play/ep" + CurrentPlayItem.ep_id;
            }
            _postion = 0;
            await playerHelper.ReportHistory(CurrentPlayItem, 0);
            await SetDanmaku();
            await SetQuality();
            var subtitle_info = await playerHelper.GetSubtitles(CurrentPlayItem.avid, CurrentPlayItem.cid);
            if (subtitle_info.subtitles != null && subtitle_info.subtitles.Count != 0)
            {
                var menu = new MenuFlyout();
                foreach (var item in subtitle_info.subtitles)
                {
                    ToggleMenuFlyoutItem menuitem = new ToggleMenuFlyoutItem() { Text = item.lan_doc, Tag = item.subtitle_url };
                    menuitem.Click += Menuitem_Click;
                    menu.Items.Add(menuitem);
                }
                ToggleMenuFlyoutItem noneItem = new ToggleMenuFlyoutItem() { Text = "无" };
                noneItem.Click += Menuitem_Click;
                menu.Items.Add(noneItem);
                (menu.Items[0] as ToggleMenuFlyoutItem).IsChecked = true;
                SetSubTitle((menu.Items[0] as ToggleMenuFlyoutItem).Tag.ToString());
                BottomBtnSelctSubtitle.Flyout = menu;
                BottomBtnSelctSubtitle.Visibility = Visibility.Visible;
                BorderSubtitle.Visibility = Visibility.Collapsed;
            }
            else
            {
                var menu = new MenuFlyout();
                menu.Items.Add(new ToggleMenuFlyoutItem() { Text = "无", IsChecked = true });
                BottomBtnSelctSubtitle.Flyout = menu;
                BottomBtnSelctSubtitle.Visibility = Visibility.Collapsed;
                BorderSubtitle.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 字幕文件
        /// </summary>
        SubtitleModel subtitles;
        /// <summary>
        /// 字幕Timer
        /// </summary>
        DispatcherTimer subtitleTimer;
        /// <summary>
        /// 选择字幕
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Menuitem_Click(object sender, RoutedEventArgs e)
        {

            foreach (ToggleMenuFlyoutItem item in (BottomBtnSelctSubtitle.Flyout as MenuFlyout).Items)
            {
                item.IsChecked = false;
            }
            var menuitem = (sender as ToggleMenuFlyoutItem);
            if (menuitem.Text == "无")
            {
                ClearSubTitle();
            }
            else
            {
                SetSubTitle(menuitem.Tag.ToString());
            }
            menuitem.IsChecked = true;
        }
        /// <summary>
        /// 设置字幕文件
        /// </summary>
        /// <param name="url"></param>
        private async void SetSubTitle(string url)
        {
            try
            {
                subtitles = await playerHelper.GetSubtitle(url);
                if (subtitles != null)
                {
                    subtitleTimer = new DispatcherTimer();
                    subtitleTimer.Interval = TimeSpan.FromMilliseconds(100);
                    subtitleTimer.Tick += SubtitleTimer_Tick;
                    subtitleTimer.Start();
                }
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("加载字幕失败了");
            }


        }

        private void SubtitleTimer_Tick(object sender, object e)
        {
            if (Player.PlayState == PlayState.Playing)
            {
                if (subtitles == null)
                {
                    return;
                }
                var time = Player.Position;
                var first = subtitles.body.FirstOrDefault(x => x.from <= time && x.to >= time);
                if (first != null)
                {
                    if (first.content != TxtSubtitle.Text)
                    {
                        BorderSubtitle.Visibility = Visibility.Visible;
                        TxtSubtitle.Text = first.content;
                    }
                }
                else
                {
                    BorderSubtitle.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ClearSubTitle()
        {
            if (subtitles != null)
            {
                if (subtitleTimer != null)
                {
                    subtitleTimer.Stop();
                    subtitleTimer = null;
                }
                BorderSubtitle.Visibility = Visibility.Collapsed;
                subtitles = null;
            }
        }



        public void ChangePlayIndex(int index)
        {
            EpisodeList.SelectedIndex = index;
        }
        private async void EpisodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EpisodeList.SelectedItem == null)
            {
                return;
            }
            DanmuControl.ClearAll();
            await SetPlayItem(EpisodeList.SelectedIndex);
        }
        double _postion = 0;
        bool _autoPlay = false;
        private async Task SetDanmaku(bool update = false)
        {
            try
            {
                if (danmakuPool != null)
                {
                    danmakuPool.Clear();
                }
                danmakuPool = await danmakuParse.ParseBiliBili(Convert.ToInt64(CurrentPlayItem.cid));
                TxtDanmuCount.Text = danmakuPool.Count.ToString();
                if (update)
                {
                    Utils.ShowMessageToast($"更新弹幕成功,共{danmakuPool.Count}条");
                }
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("弹幕加载失败");
            }
        }
        private async Task SetQuality()
        {
            VideoLoading.Visibility = Visibility.Visible;
            if (playUrlInfo != null && playUrlInfo.current != null)
            {
                playUrlInfo.current = null;
            }
            var qn = SettingHelper.GetValue<int>(SettingHelper.Player.DEFAULT_QUALITY, 80);
            var info = await playerHelper.GetPlayUrls(CurrentPlayItem, qn);

            if (info.success)
            {
                playUrlInfo = info.data;
                BottomCBQuality.ItemsSource = playUrlInfo.quality;
                BottomCBQuality.SelectionChanged -= BottomCBQuality_SelectionChanged;
                BottomCBQuality.SelectedItem = info.data.current;
                //SettingHelper.SetValue<int>(SettingHelper.Player.DEFAULT_QUALITY, info.data.current.quality);
                BottomCBQuality.SelectionChanged += BottomCBQuality_SelectionChanged;
                ChangeQuality(info.data.current, _autoPlay);
            }
            else
            {
                ShowDialog(info.message, "读取视频播放地址失败");
            }

        }
        QualityWithPlayUrlInfo current_quality_info = null;
        private async Task ChangeQuality(QualityWithPlayUrlInfo quality, bool autoplay)
        {
            VideoLoading.Visibility = Visibility.Visible;
            if (quality == null)
            {
                return;
            }
            current_quality_info = quality;
            if (quality.playUrlInfo == null)
            {
                var info = await playerHelper.GetPlayUrls(CurrentPlayItem, quality.quality);
                if (!info.success)
                {
                    ShowDialog(info.message, "切换清晰度失败");
                    return;
                }
                if (info.data.current.playUrlInfo == null)
                {
                    ShowDialog("无法读取到播放地址，试试换个清晰度?", "播放失败");
                    return;
                }
                quality.playUrlInfo = info.data.current.playUrlInfo;
            }
            PlayerOpenResult result = new PlayerOpenResult()
            {
                result = false
            };
            if (quality.playUrlInfo.mode == VideoPlayMode.Dash)
            {
                result = await Player.PlayerDashUseNative(quality.playUrlInfo.dash_video_url, quality.playUrlInfo.dash_audio_url, positon: _postion);
                if (!result.result)
                {
                    result = await Player.PlayDashUseFFmpegInterop(quality.playUrlInfo.dash_video_url, quality.playUrlInfo.dash_audio_url, positon: _postion);
                }
            }
            else if (quality.playUrlInfo.mode == VideoPlayMode.SingleFlv)
            {
                result = await Player.PlaySingleFlvUseSYEngine(quality.playUrlInfo.multi_flv_url[0].url, positon: _postion,epId:CurrentPlayItem.ep_id);
                if (!result.result)
                {
                    result = await Player.PlaySingleFlvUseFFmpegInterop(quality.playUrlInfo.multi_flv_url[0].url, positon: _postion);
                }
            }
            else if (quality.playUrlInfo.mode == VideoPlayMode.MultiFlv)
            {
                result = await Player.PlayVideoUseSYEngine(quality.playUrlInfo.multi_flv_url, positon: _postion, epId: CurrentPlayItem.ep_id);
            }
            if (result.result)
            {

                //var text = $"AID:{CurrentPlayItem.avid}\r\nCID:{CurrentPlayItem.cid}\r\nSeasonID:{CurrentPlayItem.season_id}\r\n";
                //txtInfo.Text = Player.MediaInfo;
                if (autoplay)
                {
                    Player.Play();
                }
            }
            else
            {
                ShowDialog($"播放失败:{result.message}\r\n你可以进行以下尝试:\r\n1、更换视频清晰度\r\n2、在播放设置打开/关闭硬解视频\r\n3、在播放设置中更换视频类型\r\n4、如果你的视频类型选择了MP4-HEVC，请检查是否安装了HEVC扩展", "播放失败");
            }
        }

        private async void ShowDialog(string content, string title)
        {
            MessageDialog dislog = new MessageDialog(content, title);
            await dislog.ShowAsync();
        }

        #region 全屏处理
        public void FullScreen(bool fullScreen)
        {
            
            ApplicationView view = ApplicationView.GetForCurrentView();
            FullScreenEvent?.Invoke(this, fullScreen);
            if (fullScreen)
            {
                BottomBtnExitFull.Visibility = Visibility.Visible;
                BottomBtnFull.Visibility = Visibility.Collapsed;
                BottomBtnFullWindows.Visibility = Visibility.Collapsed;
                BottomBtnExitFullWindows.Visibility = Visibility.Collapsed;
                //全屏
                if (!view.IsFullScreenMode)
                {
                    view.TryEnterFullScreenMode();
                }
            }
            else
            {
                BottomBtnExitFull.Visibility = Visibility.Collapsed;
                BottomBtnFull.Visibility = Visibility.Visible;
                if (IsFullWindow)
                {
                    BottomBtnFullWindows.Visibility = Visibility.Collapsed;
                    BottomBtnExitFullWindows.Visibility = Visibility.Visible;
                }
                else
                {
                    BottomBtnFullWindows.Visibility = Visibility.Visible;
                    BottomBtnExitFullWindows.Visibility = Visibility.Collapsed;
                }
                //退出全屏
                if (view.IsFullScreenMode)
                {
                    view.ExitFullScreenMode();
                }
            }
            BtnFoucs.Focus(FocusState.Programmatic);
        }
        public void FullWidnow(bool fullWindow)
        {
          
            if (fullWindow)
            {
                BottomBtnFullWindows.Visibility = Visibility.Collapsed;
                BottomBtnExitFullWindows.Visibility = Visibility.Visible;
            }
            else
            {
                BottomBtnFullWindows.Visibility = Visibility.Visible;
                BottomBtnExitFullWindows.Visibility = Visibility.Collapsed;
            }
            FullWindowEvent?.Invoke(this, fullWindow);
            this.Focus(FocusState.Programmatic);
        }
        private void BottomBtnExitFull_Click(object sender, RoutedEventArgs e)
        {
            IsFullScreen = false;
        }

        private void BottomBtnFull_Click(object sender, RoutedEventArgs e)
        {
            IsFullScreen = true;
        }

        private void BottomBtnExitFullWindows_Click(object sender, RoutedEventArgs e)
        {
            IsFullWindow = false;
        }

        private void BottomBtnFullWindows_Click(object sender, RoutedEventArgs e)
        {
            IsFullWindow = true;
        }
        public bool IsFullScreen
        {
            get { return (bool)GetValue(IsFullScreenProperty); }
            set { SetValue(IsFullScreenProperty, value); }
        }
        public static readonly DependencyProperty IsFullScreenProperty =
            DependencyProperty.Register("IsFullScreen", typeof(bool), typeof(PlayerControl), new PropertyMetadata(false, OnIsFullScreenChanged));
        private static void OnIsFullScreenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as PlayerControl;
            sender.FullScreen((bool)e.NewValue);
        }
        public bool IsFullWindow
        {
            get { return (bool)GetValue(IsFullWindowProperty); }
            set { SetValue(IsFullWindowProperty, value); }
        }
        public static readonly DependencyProperty IsFullWindowProperty =
            DependencyProperty.Register("IsFullWindow", typeof(bool), typeof(PlayerControl), new PropertyMetadata(false, OnIsFullWidnowChanged));
        private static void OnIsFullWidnowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as PlayerControl;
            sender.FullWidnow((bool)e.NewValue);
        }

        #endregion


        public bool ShowPlaylistButton
        {
            get { return (bool)GetValue(ShowPlaylistButtonProperty); }
            set { SetValue(ShowPlaylistButtonProperty, value); }
        }
        // Using a DependencyProperty as the backing store for ShowPlaylistButton.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShowPlaylistButtonProperty =
            DependencyProperty.Register("ShowPlaylistButton", typeof(bool), typeof(PlayerControl), new PropertyMetadata(false));

        private bool _buffering = false;
        public bool Buffering
        {
            get { return _buffering; }
            set { _buffering = value; DoPropertyChanged("Buffering"); }
        }
        private double _BufferingProgress;
        public double BufferingProgress
        {
            get { return _BufferingProgress; }
            set { _BufferingProgress = value; DoPropertyChanged("BufferingProgress"); }
        }


        private void TopBtnOpenDanmaku_Click(object sender, RoutedEventArgs e)
        {
            DanmuControl.Visibility = Visibility.Visible;
            SettingHelper.SetValue<Visibility>(SettingHelper.VideoDanmaku.SHOW, DanmuControl.Visibility);
        }

        private void TopBtnCloseDanmaku_Click(object sender, RoutedEventArgs e)
        {
            DanmuControl.Visibility = Visibility.Collapsed;
            SettingHelper.SetValue<Visibility>(SettingHelper.VideoDanmaku.SHOW, DanmuControl.Visibility);
        }
        int showControlsFlag = 0;
        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ShowControl(true);
            //FadeIn.Begin();
            //control.Visibility = Visibility.Visible;
            pointer_in_player = true;
            showControlsFlag = 0;
        }
      
        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            showControlsFlag = 3;
            pointer_in_player = false;
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            showControlsFlag = 0;
            ShowControl(true);
            //control.Visibility = Visibility.Visible;
            if (Window.Current.CoreWindow.PointerCursor == null)
            {
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            }
            
        }
        bool tapFlag;
        private async void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            tapFlag = true;
            await Task.Delay(200);
            if (tapFlag)
            {
                if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse&& !Player.Opening)
                {
                    if (Player.PlayState == PlayState.Pause || Player.PlayState == PlayState.End)
                    {
                        Player.Play();
                    }
                    else if (Player.PlayState == PlayState.Playing)
                    {
                        Player.Pause();
                    }
                }

            }

        }
        private void Grid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            tapFlag = false;
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Touch)
            {
                if (Player.PlayState == PlayState.Pause || Player.PlayState == PlayState.End)
                {
                    Player.Play();
                }
                else if (Player.PlayState == PlayState.Playing)
                {
                    Player.Pause();
                }
            }
            else
            {
                IsFullScreen = !IsFullScreen;
            }
        }

        private void BottomBtnList_Click(object sender, RoutedEventArgs e)
        {
            SettingPivot.SelectedIndex = 0;
            SplitView.IsPaneOpen = true;
        }

        private void TopBtnSettingDanmaku_Click(object sender, RoutedEventArgs e)
        {
            SettingPivot.SelectedIndex = 1;
            SplitView.IsPaneOpen = true;
        }

        private void TopBtnMore_Click(object sender, RoutedEventArgs e)
        {
            SettingPivot.SelectedIndex = 2;
            SplitView.IsPaneOpen = true;
        }



        private async void BottomCBQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BottomCBQuality.SelectedItem == null)
            {
                return;
            }

            _postion = Player.Position;
            var data = BottomCBQuality.SelectedItem as QualityWithPlayUrlInfo;
            SettingHelper.SetValue<int>(SettingHelper.Player.DEFAULT_QUALITY, data.quality);
            await ChangeQuality(data, Player.PlayState == PlayState.Playing);

        }

        private void BottomBtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (Player.Opening)
            {
                return;
            }
            Player.Pause();
            DanmuControl.PauseDanmaku();
        }

        private void BottomBtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (Player.Opening)
            {
                return;
            }
            if (Player.PlayState== PlayState.Pause|| Player.PlayState == PlayState.End)
            {
                Player.Play();
                DanmuControl.ResumeDanmaku();
            }
        }
    

        private void BottomCBSpeed_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Player.SetRate(Convert.ToDouble((BottomCBSpeed.SelectedItem as ComboBoxItem).Content.ToString().Replace("x", "")));
        }

        private void BottomBtnNext_Click(object sender, RoutedEventArgs e)
        {
            EpisodeList.SelectedIndex = EpisodeList.SelectedIndex + 1;
        }

        private void Player_PlayStateChanged(object sender, PlayState e)
        {
            switch (e)
            {
                case PlayState.Loading:
                    if (_systemMediaTransportControls != null)
                    {
                        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                    }
                    BottomBtnLoading.Visibility = Visibility.Visible;
                    BottomBtnPlay.Visibility = Visibility.Collapsed;
                    BottomBtnPause.Visibility = Visibility.Collapsed;
                    break;
                case PlayState.Playing:
                    if (_systemMediaTransportControls != null)
                    {
                        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    }
                    BottomBtnLoading.Visibility = Visibility.Collapsed;
                    BottomBtnPlay.Visibility = Visibility.Collapsed;
                    BottomBtnPause.Visibility = Visibility.Visible;
                    DanmuControl.ResumeDanmaku();
                    break;
                case PlayState.Pause:
                    if (_systemMediaTransportControls != null)
                    {
                        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    }
                    BottomBtnLoading.Visibility = Visibility.Collapsed;
                    BottomBtnPlay.Visibility = Visibility.Visible;
                    BottomBtnPause.Visibility = Visibility.Collapsed;
                    DanmuControl.PauseDanmaku();
                    break;
                case PlayState.End:
                    if (_systemMediaTransportControls != null)
                    {
                        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    }
                    BottomBtnLoading.Visibility = Visibility.Collapsed;
                    BottomBtnPlay.Visibility = Visibility.Visible;
                    BottomBtnPause.Visibility = Visibility.Collapsed;
                    break;
                case PlayState.Error:
                    if (_systemMediaTransportControls != null)
                    {
                        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Closed;
                    }
                    BottomBtnLoading.Visibility = Visibility.Visible;
                    BottomBtnPlay.Visibility = Visibility.Collapsed;
                    BottomBtnPause.Visibility = Visibility.Collapsed;
                    break;
                default:
                    break;
            }
        }

        private void Player_PlayBufferStart(object sender, EventArgs e)
        {
            Buffering = true;
            GridBuffering.Visibility = Visibility.Visible;
            TxtBuffering.Text = "正在缓冲...";
            BufferingProgress = 0;
            DanmuControl.PauseDanmaku();
        }

        private void Player_PlayBuffering(object sender, double e)
        {
            Buffering = true;
            GridBuffering.Visibility = Visibility.Visible;
            TxtBuffering.Text = "正在缓冲" + e.ToString("p");
            BufferingProgress = e;
        }

        private void Player_PlayBufferEnd(object sender, EventArgs e)
        {
            GridBuffering.Visibility = Visibility.Collapsed;
            Buffering = false;
            DanmuControl.ResumeDanmaku();
        }

        private void Player_PlayMediaEnded(object sender, EventArgs e)
        {
            if (CurrentPlayItem.is_interaction)
            {
                //TODO 互动视频
                return;
            }
            //列表顺序播放
            if (PlayerSettingPlayMode.SelectedIndex == 0)
            {
                if (CurrentPlayIndex == PlayInfos.Count - 1)
                {
                    Utils.ShowMessageToast("播放完毕");
                }
                else
                {
                    _autoPlay = true;
                    ChangePlayIndex(CurrentPlayIndex + 1);
                }
                return;
            }
            //单P循环
            if (PlayerSettingPlayMode.SelectedIndex == 1)
            {
                Player.Play();
                return;
            }
            //列表循环播放
            if (PlayerSettingPlayMode.SelectedIndex == 2)
            {
                _autoPlay = true;
                if (CurrentPlayIndex == PlayInfos.Count - 1)
                {
                    ChangePlayIndex(0);
                }
                else
                {
                    ChangePlayIndex(CurrentPlayIndex + 1);
                }
                return;
            }


        }

        private void Player_PlayMediaError(object sender, string e)
        {
            ShowDialog(e, "播放失败");
        }

        private async void DanmuSettingUpdateDanmaku_Click(object sender, RoutedEventArgs e)
        {
            await SetDanmaku(true);
        }

        private async void TopBtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            await CaptureVideo();
        }

        private async Task CaptureVideo()
        {
            try
            {
                string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg";
                StorageFolder applicationFolder = KnownFolders.PicturesLibrary;
                StorageFolder folder = await applicationFolder.CreateFolderAsync("哔哩哔哩截图", CreationCollisionOption.OpenIfExists);
                StorageFile saveFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                RenderTargetBitmap bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(Player);
                var pixelBuffer = await bitmap.GetPixelsAsync();
                using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Ignore,
                         (uint)bitmap.PixelWidth,
                         (uint)bitmap.PixelHeight,
                         DisplayInformation.GetForCurrentView().LogicalDpi,
                         DisplayInformation.GetForCurrentView().LogicalDpi,
                         pixelBuffer.ToArray());
                    await encoder.FlushAsync();
                }
                Utils.ShowMessageToast("截图已经保存至图片库");
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("截图失败");
            }
        }

        private async void Player_ChangeEngine(object sender, ChangePlayerEngine e)
        {
            if (!e.need_change)
            {
                ShowDialog($"播放失败:{e.message}\r\n你可以进行以下尝试:\r\n1、更换视频清晰度\r\n2、在播放设置打开/关闭硬解视频\r\n3、在播放设置中更换视频类型\r\n4、如果你的视频类型选择了MP4-HEVC，请检查是否安装了HEVC扩展", "播放失败");
                return;
            }
            VideoLoading.Visibility = Visibility.Visible;
            PlayerOpenResult result = new PlayerOpenResult()
            {
                result = false,
                message = ""
            };
            if (e.play_type == PlayMediaType.Dash && e.change_engine == PlayEngine.FFmpegInteropMSS)
            {
                result = await Player.PlayDashUseFFmpegInterop(current_quality_info.playUrlInfo.dash_video_url, current_quality_info.playUrlInfo.dash_audio_url, positon: _postion);
            }
            if (e.play_type == PlayMediaType.Single && e.change_engine == PlayEngine.SYEngine)
            {
                result = await Player.PlaySingleFlvUseSYEngine(current_quality_info.playUrlInfo.multi_flv_url[0].url, positon: _postion);
            }
            if (!result.result)
            {
                ShowDialog(result.message, "播放失败");
                return;
            }

        }

        private void Player_PlayMediaOpened(object sender, EventArgs e)
        {
            txtInfo.Text = Player.GetMediaInfo();
            VideoLoading.Visibility = Visibility.Collapsed;
        }

        private async void BottomBtnSendDanmakuWide_Click(object sender, RoutedEventArgs e)
        {
            Player.Pause();
            SendDanmakuDialog sendDanmakuDialog = new SendDanmakuDialog(CurrentPlayItem.avid, CurrentPlayItem.cid, Player.Position);
            sendDanmakuDialog.DanmakuSended += new EventHandler<SendDanmakuModel>((obj, arg) =>
            {
                DanmuControl.AddDanmu(new DanmakuModel()
                {
                    color = NSDanmaku.Utils.ToColor(arg.color),
                    text = arg.text,
                    location = (DanmakuLocation)arg.location,
                    size = 25,
                    time = Player.Position
                }, true);
            });
            await sendDanmakuDialog.ShowAsync();
            Player.Play();
        }

        private async void DanmuSettingSyncWords_Click(object sender, RoutedEventArgs e)
        {
            await settingVM.SyncDanmuFilter();
        }

        private async void DanmuSettingAddWord_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DanmuSettingTxtWord.Text))
            {
                Utils.ShowMessageToast("关键词不能为空");
                return;
            }
            settingVM.ShieldWords.Add(DanmuSettingTxtWord.Text);
            SettingHelper.SetValue(SettingHelper.VideoDanmaku.SHIELD_WORD, settingVM.ShieldWords);
            var result = await settingVM.AddDanmuFilterItem(DanmuSettingTxtWord.Text, 0);
            DanmuSettingTxtWord.Text = "";
            if (!result)
            {
                Utils.ShowMessageToast("已经添加到本地，但远程同步失败");
            }
        }

        public async void Dispose()
        {
            if (CurrentPlayItem != null)
            {
                await playerHelper.ReportHistory(CurrentPlayItem, Convert.ToInt32(Player.Position));
            }

            Player.PlayStateChanged -= Player_PlayStateChanged;
            Player.PlayMediaEnded -= Player_PlayMediaEnded;
            Player.PlayMediaError -= Player_PlayMediaError;
            Player.PlayBufferEnd -= Player_PlayBufferEnd;
            Player.PlayBufferStart -= Player_PlayBufferStart;
            Player.PlayBuffering -= Player_PlayBuffering;
            Player.Dispose();
            if (danmuTimer != null)
            {
                danmuTimer.Stop();
                danmuTimer = null;
            }
            if (dispRequest != null)
            {
                dispRequest = null;
            }
        }


        private void GridViewSelectColor_ItemClick(object sender, ItemClickEventArgs e)
        {
            SendDanmakuColorText.Text = e.ClickedItem.ToString();
        }

        private void SendDanmakuTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key== Windows.System.VirtualKey.Enter)
            {
                SendDanmaku();
            }
        }

        private void SendDanmakuButton_Click(object sender, RoutedEventArgs e)
        {
            SendDanmaku();
        }
        private async void SendDanmaku()
        {
            int modeInt = 1;
            var location = DanmakuLocation.Roll;
            if (SendDanmakuMode.SelectedIndex == 2)
            {
                modeInt = 4;
                location = DanmakuLocation.Bottom;
            }
            if (SendDanmakuMode.SelectedIndex == 1)
            {
                modeInt = 5;
                location = DanmakuLocation.Top;
            }
            var color = "16777215";
            if (SendDanmakuColorBorder.Background!=null)
            {
                color = Convert.ToInt32((SendDanmakuColorBorder.Background as SolidColorBrush).Color.ToString().Replace("#FF", ""), 16).ToString();
            }
            
            var result= await playerHelper.SendDanmaku(CurrentPlayItem.avid, CurrentPlayItem.cid, SendDanmakuTextBox.Text, modeInt, color);
            if (result)
            {
                DanmuControl.AddDanmu(new DanmakuModel()
                {
                    color = NSDanmaku.Utils.ToColor(color),
                    text = SendDanmakuTextBox.Text,
                    location = location,
                    size = 25,
                    time = Player.Position
                }, true);
                SendDanmakuTextBox.Text = "";
            }

        }
    }
    public class CompareDanmakuModel : IEqualityComparer<DanmakuModel>
    {
        public bool Equals(DanmakuModel x, DanmakuModel y)
        {
            return x.text == y.text;
        }
        public int GetHashCode(DanmakuModel obj)
        {
            return obj.text.GetHashCode();
        }
    }
}
