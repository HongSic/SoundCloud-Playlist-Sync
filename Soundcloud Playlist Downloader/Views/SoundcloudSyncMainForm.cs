﻿using System;
using System.Configuration;
using System.Deployment.Application;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Taskbar;
using Soundcloud_Playlist_Downloader.Language;
using Soundcloud_Playlist_Downloader.Properties;
using Soundcloud_Playlist_Downloader.Utils;

namespace Soundcloud_Playlist_Downloader.Views
{
    public partial class SoundcloudSyncMainForm : Form
    {
        private static bool Highqualitysong;
        private static bool ConvertToMp3;
        private static int SyncMethod = 1;
        private static EnumUtil.DownloadMode _dlMode;
        private static bool FoldersPerArtist;
        private static bool ReplaceIllegalCharacters;
        private static bool ExcludeM4A;
        private static bool ExcludeAac;
        private static bool CreatePlaylists;
        private static bool MergePlaylists;
        private static int ConcurrentDownloads;
        private static bool ConfigStateActive;
        private static int ConfigStateCurrentIndex = 1;
        private static string FormatForName = "%user% - %title% %quality%";
        private static string FormatForTag = "%user% - %title% %quality%";
        
        private readonly BoxAbout _aboutWindow = new BoxAbout();
        private readonly API_Config _apiConfigSettings;

        private readonly PerformStatusUpdate _performStatusUpdateImplementation;

        private readonly PerformSyncComplete _performSyncCompleteImplementation;
        private readonly ProgressBarUpdate _progressBarUpdateImplementation;
        private ProgressUtils progressUtil;
        private ClientIDsUtils clientIdUtil;
        private UpdateUtils updateUtil;

        public SoundcloudSyncMainForm()
        {
            InitializeComponent();

            updateUtil = new UpdateUtils();
            updateToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_UPDATE"] + updateUtil.LabelTextForCurrentStatus();

            clientIdUtil = new ClientIDsUtils();
            _apiConfigSettings = new API_Config(clientIdUtil);
            progressUtil = new ProgressUtils();

            Text = string.Format(LanguageManager.Language["STR_MAIN_TITLE_STABLE"], Version());
            _performSyncCompleteImplementation = SyncCompleteButton;
            _progressBarUpdateImplementation = UpdateProgressBar;
            _performStatusUpdateImplementation = UpdateStatus;
            status.Tag = "STR_MAIN_STATUS_READY";
            status.Text = LanguageManager.Language[status.Tag.ToString()];
            MinimumSize = new Size(Width, Height);
            MaximumSize = new Size(Width, Height);
        }

        private static string Version()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                return ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString(4);
            }
            return "-";
        }

        public sealed override string Text
        {
            get { return base.Text; }
            set { base.Text = value; }
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                directoryPath.Text = dialog.SelectedPath?.ToLower();
            }
        }

        [SilentFailure]
        private void UpdateStatus()
        {
            if (!progressUtil.Exiting)
            {
                if (progressUtil.IsActive && progressBar.Value == progressBar.Maximum &&
                progressBar.Value != progressBar.Minimum)
                {
                    IsSyncButtonClicked = false;
                    status.Tag = "STR_MAIN_STATUS_COMPLETE";
                    status.Text = LanguageManager.Language[status.Tag.ToString()];
                }
                else if (progressUtil.IsActive && progressBar.Value >= progressBar.Minimum && progressBar.Maximum > 0)
                {
                    IsSyncButtonClicked = false;
                    status.Tag = "STR_MAIN_STATUS_DOWNLOAD";
                    status.Text = string.Format(LanguageManager.Language[status.Tag.ToString()], progressBar.Value, progressBar.Maximum);
                }
                else if (progressUtil.IsActive && progressUtil.Completed && !progressUtil.IsError)
                {
                    IsSyncButtonClicked = false;
                    status.Tag = "STR_MAIN_STATUS_SYNCED";
                    status.Text = LanguageManager.Language[status.Tag.ToString()];
                }
                else if (progressUtil.IsActive && progressUtil.Completed && progressUtil.IsError)
                {
                    IsSyncButtonClicked = false;
                    status.Tag = "STR_MAIN_STATUS_SYNCEDERROR";
                    status.Text = LanguageManager.Language[status.Tag.ToString()];
                }
                else if (!progressUtil.IsActive && IsSyncButtonClicked)
                {
                    IsSyncButtonClicked = false;
                    status.Tag = "STR_MAIN_STATUS_ABORTING";
                    status.Text = LanguageManager.Language[status.Tag.ToString()];
                }
                else if (progressUtil.IsActive)
                {
                    IsSyncButtonClicked = false;
                    var plural = "";
                    if (_dlMode != EnumUtil.DownloadMode.Track)
                        plural = "STR_MAIN_STATUS_FETCH_S";
                    status.Tag = new string[] { "STR_MAIN_STATUS_FETCH", plural };
                    status.Text = string.Format(LanguageManager.Language["STR_MAIN_STATUS_FETCH"], LanguageManager.Language[plural]);
                }
                else if (!progressUtil.IsActive)
                {
                    IsSyncButtonClicked = false;
                    status.Tag ="STR_MAIN_STATUS_ABORTED";
                    status.Text = LanguageManager.Language[status.Tag.ToString()];
                }
            }
            else if (progressUtil.Completed)
            {
                // the form has indicated it is being closed and the sync utility has finished aborting
                Close();
                Dispose();
            }
        }

        [SilentFailure]
        private void InvokeUpdateStatus()
        {
            statusStrip1.Invoke(_performStatusUpdateImplementation);
        }

        [SilentFailure]
        private void UpdateProgressBar()
        {
            progressBar.Minimum = 0;
            progressBar.Maximum = progressUtil.SongsToDownload;

            progressBar.Value = progressUtil.SongsDownloaded;

            TaskbarManager.Instance.SetProgressValue(progressUtil.SongsDownloaded, progressUtil.SongsToDownload);
            if (progressBar.Minimum != 0 && progressBar.Maximum == progressBar.Value)
            {
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
            }
        }

        private void InvokeUpdateProgressBar()
        {
            progressBar.Invoke(_progressBarUpdateImplementation);
        }

        private void InvokeSyncComplete()
        {
            syncButton.Invoke(_performSyncCompleteImplementation);
        }

        bool IsSyncButtonClicked;
        [SilentFailure]
        private void SyncCompleteButton()
        {
            syncButton.Tag = "STR_SYNCHRONIZE";
            syncButton.Text = LanguageManager.Language[syncButton.Tag.ToString()];
            syncButton.Enabled = true;
            if (progressUtil.Exiting)
            {
                Dispose();
            }
        }

        private void syncButton_Click(object sender, EventArgs e)
        {
            SaveSettingsToConfig(ConfigStateCurrentIndex);

            _dlMode = playlistRadio.Checked
            ? EnumUtil.DownloadMode.Playlist
            : userPlaylists.Checked ? EnumUtil.DownloadMode.UserPlaylists
            : favoritesRadio.Checked ? EnumUtil.DownloadMode.Favorites
            : artistRadio.Checked ? EnumUtil.DownloadMode.Artist : EnumUtil.DownloadMode.Track;
            if (!string.IsNullOrWhiteSpace(url.Text?.ToLower()) &&
            !string.IsNullOrWhiteSpace(directoryPath.Text?.ToLower()) &&
            !IsSyncButtonClicked)
            {
                IsSyncButtonClicked = true;
                syncButton.Tag = "STR_ABORT";
                syncButton.Text = LanguageManager.Language[syncButton.Tag.ToString()];
                status.Tag ="STR_MAIN_STATUS_CHECK";
                status.Text = LanguageManager.Language[status.Tag.ToString()];
                progressUtil.Completed = false;

                progressBar.Value = 0;
                progressBar.Maximum = 0;
                progressBar.Minimum = 0;
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);

                Highqualitysong = chk_highquality.Checked;
                ConvertToMp3 = chk_convertToMp3.Checked;

                SyncMethod = rbttn_oneWay.Checked ? 1 : 2;
                FoldersPerArtist = chk_folderByArtist.Checked;
                ReplaceIllegalCharacters = chk_replaceIllegalCharacters.Checked;
                ExcludeAac = chk_excl_m4a.Checked;
                ExcludeM4A = chk_excl_m4a.Checked;
                CreatePlaylists = chk_CreatePlaylists.Checked;
                ConcurrentDownloads = (int)nudConcurrency.Value;
                MergePlaylists = chk_MergePlaylists.Checked;

                Uri soundCloudUri;
                try
                {
                    soundCloudUri = new Uri(url?.Text?.ToLower());
                }
                catch (Exception)
                {
                    status.Tag = "STR_MAIN_STATUS_INVALIDURL";
                    status.Text = LanguageManager.Language[status.Tag.ToString()];
                    progressUtil.Completed = true;
                    InvokeSyncComplete();
                    return;
                }

                var filesystemUtil = new FilesystemUtils(new DirectoryInfo(directoryPath?.Text?.ToLower()), trackRadio.Checked ? FormatForTag : FormatForName, FoldersPerArtist, ReplaceIllegalCharacters);
                var manifestUtil = new ManifestUtils(progressUtil, filesystemUtil, soundCloudUri, _dlMode, SyncMethod);
                var playlistUtil = new PlaylistUtils(manifestUtil);
                DownloadUtils downloadUtil = new DownloadUtils(clientIdUtil, ExcludeM4A, ExcludeAac, ConvertToMp3, manifestUtil, Highqualitysong, ConcurrentDownloads);
                var syncUtil = new SyncUtils(CreatePlaylists, manifestUtil, downloadUtil, playlistUtil);
                if (_dlMode != EnumUtil.DownloadMode.Track)
                {
                    bool differentmanifest;
                    if (!manifestUtil.FindManifestAndBackup(out differentmanifest))
                    {
                        if (differentmanifest)
                        {
                            status.Tag = "STR_MAIN_STATUS_DIFFMANY";
                            status.Text = LanguageManager.Language[status.Tag.ToString()];
                            progressUtil.Completed = true;
                            InvokeSyncComplete();
                            return;
                        }
                    }
                }
                new Thread(() =>
                {
                    // perform progress updates
                    while (!progressUtil.Completed && !progressUtil.Exiting)
                    {
                        Thread.Sleep(500);
                        InvokeUpdateStatus();

                        this.Invoke((MethodInvoker)(() => lb_progressOfTracks.DataSource = progressUtil.GetTrackProgressValues()));
                        this.Invoke((MethodInvoker)(() => lb_progressOfTracks.Refresh()));

                        InvokeUpdateProgressBar();
                    }
                    if (!progressUtil.Exiting)
                    {
                        InvokeUpdateStatus();
                    }
                }).Start();

                new Thread(() =>
                {
                    try
                    {
                        var sync = new SoundcloudSync(syncUtil, MergePlaylists);
                        sync.Synchronize(url?.Text?.ToLower());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{ex.Message} { ExceptionHandlerUtils.GetInnerExceptionMessages(ex)}", LanguageManager.Language["STR_ERR"]);
                    }
                    finally
                    {
                        progressUtil.Completed = true;
                        InvokeSyncComplete();
                    }
                }).Start();
            }
            else if (progressUtil.IsActive && IsSyncButtonClicked)
            {
                progressUtil.IsActive = false;
                syncButton.Enabled = false;
            }
            else if (!IsSyncButtonClicked && string.IsNullOrWhiteSpace(url.Text))
            {
                status.Tag = "STR_MAIN_STATUS_NULLURL";
                status.Text = LanguageManager.Language[status.Tag.ToString()];
            }
            else if (!IsSyncButtonClicked && string.IsNullOrWhiteSpace(directoryPath.Text))
            {
                status.Tag = "STR_MAIN_STATUS_NULLDIR";
                status.Text = LanguageManager.Language[status.Tag.ToString()];
            }
        }

        [SilentFailure]
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            progressUtil.Exiting = true;
            progressUtil.IsActive = false;
            status.Tag = "STR_MAIN_STATUS_EXIT";
            status.Text = LanguageManager.Language[status.Tag.ToString()];
            syncButton.Enabled = false;
            if (IsSyncButtonClicked)
            {
                if(MessageBox.Show(LanguageManager.Language["STR_MAIN_STATUS_SYNCING"], this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    e.Cancel = true;
            }
            else
            {
                IsSyncButtonClicked = true;
                syncButton.Tag = "STR_ABORT";
                syncButton.Text = LanguageManager.Language[syncButton.Tag.ToString()];
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Icon = Resources.MainIcon;
            LoadSettingsFromCurrentConfig(Settings.Default.ConfigStateCurrentIndex);
            LoadSetting();
            if (toolStripComboBox1.SelectedIndex == -1) toolStripComboBox1.SelectedIndex = 0;
        }

        private void SaveSettingsToConfig(int currentIndex)
        {
            Settings.Default.ConfigStateCurrentIndex = currentIndex;
            SaveSettingToConfig(chk_configActive.Name, chk_configActive.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig("Language", toolStripComboBox1.SelectedIndex.ToString(), typeof(int));
            SaveSettingToConfig("LocalPath", directoryPath?.Text, directoryPath?.Text?.GetType());
            SaveSettingToConfig("PlaylistUrl", url?.Text, url?.Text?.GetType());
            SaveSettingToConfig(nameof(ConcurrentDownloads), nudConcurrency.Value.ToString(), nudConcurrency.Value.GetType());
            SaveSettingToConfig(favoritesRadio.Name, favoritesRadio.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig("playlistRadio", playlistRadio.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(userPlaylists.Name, userPlaylists.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(artistRadio.Name, artistRadio.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(trackRadio.Name, trackRadio.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_convertToMp3.Name, chk_convertToMp3.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_excl_m4a.Name, chk_excl_m4a.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_exl_aac.Name, chk_exl_aac.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_folderByArtist.Name, chk_folderByArtist.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_highquality.Name, chk_highquality.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_replaceIllegalCharacters.Name, chk_replaceIllegalCharacters.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(rbttn_oneWay.Name, rbttn_oneWay.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(rbttn_twoWay.Name, rbttn_twoWay.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_MergePlaylists.Name, chk_MergePlaylists.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(chk_CreatePlaylists.Name, chk_CreatePlaylists.Checked.ToString(), typeof(Boolean));
            SaveSettingToConfig(nameof(FormatForName), FormatForName, typeof(String));
            SaveSettingToConfig(nameof(FormatForTag), FormatForTag, typeof(String));
            Settings.Default.Save();
        }

        public void SaveSettingToConfig(string propertyName, string propertyValue, Type propertyType)
        {
            string accessString = GetAccessString(Settings.Default.ConfigStateCurrentIndex);
            switch (Type.GetTypeCode(propertyType))
            {
                case TypeCode.Boolean:
                    Settings.Default[accessString + propertyName] = Boolean.Parse(propertyValue);
                    break;
                case TypeCode.Decimal:
                    Settings.Default[accessString + propertyName] = Int32.Parse(propertyValue);
                    break;
                case TypeCode.String:
                    Settings.Default[accessString + propertyName] = propertyValue.ToLower();
                    break;
                default:
                    break;
            }
        }

        private void LoadSettingsFromCurrentConfig(int currentIndex)
        {
            Settings.Default.ConfigStateCurrentIndex = currentIndex;
            string accessString = GetAccessString(currentIndex);
            lbl_currentConfig.Text = Settings.Default.ConfigStateCurrentIndex.ToString();

            //toolStripComboBox1.SelectedIndex = (int)LoadSettingFromConfig(accessString, "Language", typeof(int));
            chk_configActive.Checked = (bool) LoadSettingFromConfig(accessString, chk_configActive.Name, typeof(Boolean));
            url.Text = (string)LoadSettingFromConfig(accessString, "PlaylistUrl", typeof(String));
            directoryPath.Text = (string)LoadSettingFromConfig(accessString, "LocalPath", typeof(String));
            nudConcurrency.Value = (int)LoadSettingFromConfig(accessString, nameof(ConcurrentDownloads), typeof(Int32));
            favoritesRadio.Checked = (bool)LoadSettingFromConfig(accessString, favoritesRadio.Name, typeof(Boolean));
            userPlaylists.Checked = (bool)LoadSettingFromConfig(accessString, userPlaylists.Name, typeof(Boolean));
            playlistRadio.Checked = (bool)LoadSettingFromConfig(accessString, "playlistRadio", typeof(Boolean));
            artistRadio.Checked = (bool)LoadSettingFromConfig(accessString, artistRadio.Name, typeof(Boolean));
            trackRadio.Checked = (bool)LoadSettingFromConfig(accessString, trackRadio.Name, typeof(Boolean));
            chk_convertToMp3.Checked = (bool)LoadSettingFromConfig(accessString, chk_convertToMp3.Name, typeof(Boolean));
            chk_excl_m4a.Checked = (bool)LoadSettingFromConfig(accessString, chk_excl_m4a.Name, typeof(Boolean));
            chk_exl_aac.Checked = (bool)LoadSettingFromConfig(accessString, chk_exl_aac.Name, typeof(Boolean));
            chk_folderByArtist.Checked = (bool)LoadSettingFromConfig(accessString, chk_folderByArtist.Name, typeof(Boolean));
            chk_highquality.Checked = (bool)LoadSettingFromConfig(accessString, chk_highquality.Name, typeof(Boolean));
            chk_replaceIllegalCharacters.Checked = (bool)LoadSettingFromConfig(accessString, chk_replaceIllegalCharacters.Name, typeof(Boolean));
            chk_CreatePlaylists.Checked = (bool)LoadSettingFromConfig(accessString, chk_CreatePlaylists.Name, typeof(Boolean));
            chk_MergePlaylists.Checked = (bool)LoadSettingFromConfig(accessString, chk_MergePlaylists.Name, typeof(Boolean));
            rbttn_oneWay.Checked = (bool)LoadSettingFromConfig(accessString, rbttn_oneWay.Name, typeof(Boolean));
            rbttn_twoWay.Checked = (bool)LoadSettingFromConfig(accessString, rbttn_twoWay.Name, typeof(Boolean));
            FormatForName = (string)LoadSettingFromConfig(accessString, nameof(FormatForName), typeof(String));
            FormatForTag = (string)LoadSettingFromConfig(accessString, nameof(FormatForTag), typeof(String));
        }

        public object LoadSettingFromConfig(string accessString, string propertyName, Type propertyType)
        {
            try
            {
                return Settings.Default[accessString + propertyName];
            }
            catch (SettingsPropertyNotFoundException)
            {
                var property = new SettingsProperty(accessString + propertyName)
                {
                    DefaultValue = LoadSettingFromConfig("", propertyName, propertyType),
                    IsReadOnly = false,
                    PropertyType = propertyType,
                    Provider = Settings.Default.Providers["LocalFileSettingsProvider"],
                };
                property.Attributes.Add(typeof(System.Configuration.UserScopedSettingAttribute), new System.Configuration.UserScopedSettingAttribute());
                Settings.Default.Properties.Add(property);
                Settings.Default.Save();
                return property.DefaultValue;
            }
        }


        private void SaveSetting()
        {
            string[] s = new string[1];
            s[0] = "Language=" + toolStripComboBox1.SelectedIndex;

            try { File.WriteAllLines("Settings.ini", s); } catch { }
        }
        private void LoadSetting()
        {
            try
            {
                string[] s = File.ReadAllLines("Settings.ini");
                for(int i = 0; i < s.Length; i++)
                {
                    int index = s[i].IndexOf("=");
                    if (index > 0)
                    {
                        string key = s[i].Remove(index);
                        string value = s[i].Substring(index + 1);

                        if ("Language".Equals(key)) try { toolStripComboBox1.SelectedIndex = int.Parse(value); } catch { }
                    }
                }
            }
            catch { }
        }


        private string GetAccessString(int currentIndex)
        {
            string accessString = "";
            if (currentIndex != 1)
                accessString = ConfigStateCurrentIndex.ToString();
            return accessString;
        }

        private void aboutToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (_aboutWindow.Visible)
            {
                _aboutWindow.Focus();
            }
            else
            {
                _aboutWindow.Show();
            }
        }

        private void chk_convertToMp3_CheckedChanged(object sender, EventArgs e)
        {
            if (chk_convertToMp3.Checked)
            {
                chk_excl_m4a.Visible = true;
                chk_exl_aac.Visible = true;
                lbl_exclude.Visible = true;
            }
            else
            {
                chk_excl_m4a.Visible = false;
                chk_excl_m4a.Checked = false;
                chk_exl_aac.Visible = false;
                chk_exl_aac.Checked = false;
                lbl_exclude.Visible = false;
            }
        }

        private void chk_highquality_CheckedChanged(object sender, EventArgs e)
        {
            if (chk_highquality.Checked)
            {
                chk_convertToMp3.Enabled = true;
                chk_convertToMp3.Checked = true;
                pnl_convert.Visible = true;
            }
            else
            {
                chk_convertToMp3.Enabled = false;
                chk_convertToMp3.Checked = false;
                pnl_convert.Visible = false;
            }
        }

        private delegate void ProgressBarUpdate();

        private delegate void PerformSyncComplete();

        private delegate void PerformStatusUpdate();

        private void updateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            updateUtil.InstallUpdateSyncWithInfo();
            updateToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_UPDATE"] + updateUtil.LabelTextForCurrentStatus();
        } 

        private void rbttn_twoWay_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void gbox_downMethod_Enter(object sender, EventArgs e)
        {

        }

        private void clientIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_apiConfigSettings.Visible)
            {
                _apiConfigSettings.Focus();
            }
            else
            {
                _apiConfigSettings.Show();
            }
        }
      
        private void chk_configActive_CheckedChanged(object sender, EventArgs e)
        {
            ConfigStateActive = chk_configActive.Checked;
        }

        private void config1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettingsToConfig(ConfigStateCurrentIndex);
            lbl_currentConfig.Text = "1";
            ConfigStateCurrentIndex = 1;
            LoadSettingsFromCurrentConfig(ConfigStateCurrentIndex);
        }

        private void config2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettingsToConfig(ConfigStateCurrentIndex);
            lbl_currentConfig.Text = "2";
            ConfigStateCurrentIndex = 2;
            LoadSettingsFromCurrentConfig(ConfigStateCurrentIndex);
        }

        private void config3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettingsToConfig(ConfigStateCurrentIndex);
            lbl_currentConfig.Text = "3";
            ConfigStateCurrentIndex = 3;
            LoadSettingsFromCurrentConfig(ConfigStateCurrentIndex);
        }

        private void config4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettingsToConfig(ConfigStateCurrentIndex);
            lbl_currentConfig.Text = "4";
            ConfigStateCurrentIndex = 4;
            LoadSettingsFromCurrentConfig(ConfigStateCurrentIndex);
        }

        private void config5ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettingsToConfig(ConfigStateCurrentIndex);
            lbl_currentConfig.Text = "5";
            ConfigStateCurrentIndex = 5;
            LoadSettingsFromCurrentConfig(ConfigStateCurrentIndex);
        }

        /// <summary>
        /// Format filenames.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_FormatForName_Click(object sender, EventArgs e)
        {
            NameFormater filenameFormatter = new NameFormater(FormatForName);
            if(filenameFormatter.ShowDialog() == DialogResult.OK)
                FormatForName = string.IsNullOrWhiteSpace(filenameFormatter.Format) ? "%title%" : filenameFormatter.Format;
            SaveSettingsToConfig(ConfigStateCurrentIndex);
        }

        /// <summary>
        /// Format metedata tags. Not yet implemented
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_FormatForTag_Click(object sender, EventArgs e)
        {
            NameFormater metadataformatter = new NameFormater(FormatForTag) { Text = "Metadata Formatter (ID3)" };
            if (metadataformatter.ShowDialog() == DialogResult.OK)
                FormatForTag = string.IsNullOrWhiteSpace(metadataformatter.Format) ? "%title%" : metadataformatter.Format;
            SaveSettingsToConfig(ConfigStateCurrentIndex);
        }

        private void chk_replaceIllegalCharacters_CheckedChanged(object sender, EventArgs e)
        {

        }

        int LastSelectLannguage = 0;
        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(LastSelectLannguage != toolStripComboBox1.SelectedIndex)
            {
                LastSelectLannguage = toolStripComboBox1.SelectedIndex;
                switch(LastSelectLannguage)
                {
                    case 1: LanguageManager.Language = new LanguageManager(Resources.Language_Korean.Split(new char[] { '\n', '\r' }, StringSplitOptions.None)); break;
                    default: LanguageManager.Language = LanguageManager.GetDefault(); break;
                }
                LoadLanguage();
                _apiConfigSettings.LoadLanguage();
                _aboutWindow.LoadLanguage();

                SaveSetting();
            }
        }

        private void LoadLanguage()
        {
            Text = string.Format(LanguageManager.Language["STR_MAIN_TITLE_STABLE"], Version());
            configurationsToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_TITLE"];
            configurationsToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_CONFIGS"];
            config1ToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_CONFIG"] + " 1";
            config2ToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_CONFIG"] + " 2";
            config3ToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_CONFIG"] + " 3";
            config4ToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_CONFIG"] + " 4";
            config5ToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_CONFIG"] + " 5";
            clientIDToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_CLIENT"];
            updateToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_UPDATE"] + updateUtil.LabelTextForCurrentStatus();
            aboutToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_ABOUT"];
            languageToolStripMenuItem.Text = LanguageManager.Language["STR_MAIN_MENU_LNG"];

            tabPage_BasicOptions.Text = LanguageManager.Language["STR_MAIN_BASIC"];
            gbox_url.Text = LanguageManager.Language["STR_MAIN_BASIC_URL"];
            gbox_localdir.Text = LanguageManager.Language["STR_MAIN_BASIC_DIR"];
            browseButton.Text = LanguageManager.Language["STR_MAIN_BASIC_BROWSE"];
            gbox_downMethod.Text = LanguageManager.Language["STR_MAIN_BASIC_DM"];
            userPlaylists.Text = LanguageManager.Language["STR_MAIN_BASIC_DM1"];
            playlistRadio.Text = LanguageManager.Language["STR_MAIN_BASIC_DM2"];
            favoritesRadio.Text = LanguageManager.Language["STR_MAIN_BASIC_DM3"];
            artistRadio.Text = LanguageManager.Language["STR_MAIN_BASIC_DM4"];
            trackRadio.Text = LanguageManager.Language["STR_MAIN_BASIC_DM5"];
            gbox_syncMethod.Text = LanguageManager.Language["STR_MAIN_BASIC_SM"];
            rbttn_oneWay.Text = LanguageManager.Language["STR_MAIN_BASIC_SM1"];
            rbttn_twoWay.Text = LanguageManager.Language["STR_MAIN_BASIC_SM2"];

            if (status.Tag == null) status.Text = LanguageManager.Language["STR_MAIN_STATUS_READY"];
            else if (status.Tag is string[])
            {
                string[] s = (string[])status.Tag;
                status.Text = string.Format(LanguageManager.Language[s[0]], LanguageManager.Language[s[1]]);
            }
            else status.Text = LanguageManager.Language[status.Tag.ToString()];

            groupBox2.Text = LanguageManager.Language["STR_MAIN_CONFSTAT"];
            lbl_configurationPrefix.Text = LanguageManager.Language["STR_MAIN_CONF"];
            chk_configActive.Text = LanguageManager.Language["STR_MAIN_CONFACTIVE"];
            lbl_currentConfig.Location = new Point(lbl_configurationPrefix.Width + 5, lbl_currentConfig.Location.Y);
            chk_configActive.Location = new Point(lbl_currentConfig.Location.X + lbl_currentConfig.Width + 10, lbl_currentConfig.Location.Y);
            groupBox1.Text = LanguageManager.Language["STR_MAIN_DOWMPROG"];
            syncButton.Text = LanguageManager.Language[syncButton.Tag.ToString()];

            tabPage_AdvancedOptions.Text = LanguageManager.Language["STR_MAIN_ADVANCE"];
            gbox_advanced_conversion.Text = LanguageManager.Language["STR_MAIN_ADVANCE_CONVERSE"];
            chk_highquality.Text = LanguageManager.Language["STR_MAIN_ADVANCE_HQ"];
            chk_convertToMp3.Text = LanguageManager.Language["STR_MAIN_ADVANCE_HQ_MP3"];
            lbl_exclude.Text = LanguageManager.Language["STR_MAIN_ADVANCE_HQ_EXCL"] + ":";
            gbox_advanced_enginebehaviour.Text = LanguageManager.Language["STR_MAIN_ADVANCE_DOWNB"];
            chk_replaceIllegalCharacters.Text = LanguageManager.Language["STR_MAIN_ADVANCE_ILLIGCHAR"];
            tt_qualityExplanation.SetToolTip(chk_replaceIllegalCharacters, LanguageManager.Language["STR_MAIN_ADVANCE_ILLIGCHAR_DESC"].Replace("\\n", "\n"));
            concurrency.Text = LanguageManager.Language["STR_MAIN_ADVANCE_CONCURRENCY"] + ":";
            gbox_advanced_other.Text = LanguageManager.Language["STR_MAIN_ADVANCE_OTHER"];
            btn_FormatForName.Text = LanguageManager.Language["STR_MAIN_ADVANCE_FILEFORMAT"];
            btn_FormatForTag.Text = LanguageManager.Language["STR_MAIN_ADVANCE_METAFORMAT"];
            chk_folderByArtist.Text = LanguageManager.Language["STR_MAIN_ADVANCE_FBA"];
            chk_MergePlaylists.Text = LanguageManager.Language["STR_MAIN_ADVANCE_MSP"];
            chk_CreatePlaylists.Text = LanguageManager.Language["STR_MAIN_ADVANCE_GMPL"];
            checkBox1.Text = LanguageManager.Language["STR_MAIN_ADVANCE_MSTT"];
        }
    }
}