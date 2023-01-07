using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate,
        cancelado
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool debugmode = false;
        
        private string rootPath;
        private string versionFile;
        private string bepInZip; 
        private string gameDir;
        private string gameZip;
        private string gameExe;
        private string bepInDir;
        WebClient webClient;
        WebClient VersionClient;

        private LauncherStatus _status;
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = "Jogar";
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Tentar Novamente";
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Baixando Jogo - Cancelar";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Baixando Atualização - Cancelar";
                        break;
                    case LauncherStatus.cancelado:
                        PlayButton.Content = "Iniciar Atualização";
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            versionFile = Path.Combine(rootPath, "Version.txt");
            
            bepInZip = Path.Combine(rootPath, "ValheimMMO", "BepInEx.zip");
            bepInDir = Path.Combine(rootPath, "ValheimMMO", "BepInEx");

            gameZip = Path.Combine(rootPath, "ValheimMMO.zip");
            gameExe = Path.Combine(rootPath, "ValheimMMO", "valheim.exe");
            gameDir = Path.Combine(rootPath, "ValheimMMO");
            DownloadProgressBar.Visibility = Visibility.Hidden;

            // Mouse Handle para arrastar
            MouseDown += Window_MouseDown;

        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }


        private void CheckForUpdates()
        {
            ClearAndHideLAbels();

            if (File.Exists(versionFile))
            {
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();
                VersionClient = new WebClient();

                try
                {
                    
                    // Link para Version.txt
                    Version onlineVersion = new Version(VersionClient.DownloadString("https://onedrive.live.com/download?cid=C8B7A698B7D995F7&resid=C8B7A698B7D995F7%21139&authkey=AEFs9T-Fhrst4DM"));

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                        ClearAndHideLAbels();

                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    TBLabel.Content = "Erro ao procurar por atualizações: " + ex.Message;
                    
                    if (debugmode)
                        MessageBox.Show($"ERRO: {ex}");
                }
            }
            else
            {
                InstallGameFiles(false, Version.zero);
            }
        }

       
        private void InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            try
            {
                webClient = new WebClient();
                DownloadProgressBar.Visibility = Visibility.Visible;

                // Remove todos os Handles
                webClient.DownloadProgressChanged -= new DownloadProgressChangedEventHandler(DownloadProgressCallback);
                webClient.DownloadFileCompleted -= new AsyncCompletedEventHandler(DownloadUpdateCompletedCallback);

                // Handle Universal
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);

                if (_isUpdate)
                {
                    // Baixa Atualização
                    Status = LauncherStatus.downloadingUpdate;
                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadUpdateCompletedCallback);
                    webClient.DownloadFileAsync(new Uri("https://onedrive.live.com/download?cid=C8B7A698B7D995F7&resid=C8B7A698B7D995F7%21143&authkey=ADZIBSqTjZrUvJI"), bepInZip, _onlineVersion);
                }
                else
                {
                    // Instala o jogo
                    Status = LauncherStatus.downloadingGame;
                    VersionClient = new WebClient();
                    _onlineVersion = new Version(VersionClient.DownloadString("https://onedrive.live.com/download?cid=C8B7A698B7D995F7&resid=C8B7A698B7D995F7%21139&authkey=AEFs9T-Fhrst4DM"));
                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                    webClient.DownloadFileAsync(new Uri("https://onedrive.live.com/download?cid=C8B7A698B7D995F7&resid=C8B7A698B7D995F7%21144&authkey=AB6Qqr5-5LiEKow"), gameZip, _onlineVersion);
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                TBLabel.Content = "Erro ao instalar jogo: " + ex.Message;

                if (debugmode)
                    MessageBox.Show($"ERRO: {ex}");
            }
        }

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {

            if (Status == LauncherStatus.cancelado)
            {
                CancelAction();
                return; 
            }

                string msg = String.Format("Baixando {0} MB de {1} MB Total", Math.Round(e.BytesReceived / 1024d / 1024d), Math.Round(e.TotalBytesToReceive / 1024d / 1024d));
            DownloadProgressBar.Value = e.ProgressPercentage;
            TBLabel.Content = msg;
            TBPercentual.Content = String.Format("{0} %", e.ProgressPercentage);

            if (e.ProgressPercentage >= 99)
            {
                TBLabel.Content = "Extraindo arquivos aguarde...";
            }


            // Debug.
            /*
                Console.WriteLine("{0}    Baixando {1} de {2} bytes. {3} % completados...",
                (string)e.UserState,
                e.BytesReceived,
                e.TotalBytesToReceive,
                e.ProgressPercentage);
            */


        }


        // Download Game
        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.Cancelled)
                {
                    CancelAction();
                    return;
                }
     

                string onlineVersion = ((Version)e.UserState).ToString();
                // Deleta pasta BepinEx antiga se existir.
                if (Directory.Exists(gameDir))
                {
                    Directory.Delete(gameDir, true);
                }
                // Extrai o arquivo Zip
                TBLabel.Content = "Extraindo arquivos aguarde...";
                ZipFile.ExtractToDirectory(gameZip, rootPath, true);
                // Deleta aqueivo zip baixado
                File.Delete(gameZip);
                // Grava nova Versão
                File.WriteAllText(versionFile, onlineVersion);

                VersionText.Text = onlineVersion;
                Status = LauncherStatus.ready;
                ClearAndHideLAbels();
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                TBLabel.Content = "Erro ao terminar o download: " + ex.Message;

                if (debugmode)
                    MessageBox.Show($"ERRO: {ex}");
            }
        } 
        
        // Download Update Bepin
        private void DownloadUpdateCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.Cancelled)
                {
                    CancelAction();
                    return;
                }

                string onlineVersion = ((Version)e.UserState).ToString();
                // Deleta pasta BepinEx antiga se existir
                if (Directory.Exists(bepInDir))
                {
                    Directory.Delete(bepInDir,true);
                }
                // Extrai o arquivo Zip
                TBLabel.Content = "Extraindo arquivos aguarde...";
                ZipFile.ExtractToDirectory(bepInZip, gameDir, true);
                // Deleta aqueivo zip baixado
                File.Delete(bepInZip);
                //Grava nova Versão
                File.WriteAllText(versionFile, onlineVersion);

                VersionText.Text = onlineVersion;
                Status = LauncherStatus.ready;
                ClearAndHideLAbels();
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                TBLabel.Content = "Erro ao terminar o download: " + ex.Message;

                if (debugmode)
                    MessageBox.Show($"ERRO: {ex}");
            }
        }

        private void ClearAndHideLAbels()
        {
            TBLabel.Content = "";
            TBPercentual.Content = "";

            DownloadProgressBar.Visibility = Visibility.Hidden;

        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            TBLabel.Content = "";
            
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(rootPath, "ValheimMMO");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
            else if (Status == LauncherStatus.downloadingGame)
            {
                Status = LauncherStatus.cancelado;
            }
            else if (Status == LauncherStatus.downloadingUpdate)
            {
                Status = LauncherStatus.cancelado;
            }
            else if (Status == LauncherStatus.cancelado)
            {
                CheckForUpdates();
            }
        }

        public virtual void CancelAction()
        {
            if (this.webClient != null)
            {
                webClient.CancelAsync();
                webClient.Dispose();
            }

            if (this.VersionClient != null)
            {
                VersionClient.CancelAsync();
                VersionClient.Dispose();
            }

            ClearAndHideLAbels();
           

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }

    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

        internal Version(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }
        internal Version(string _version)
        {
            string[] versionStrings = _version.Split('.');
            if (versionStrings.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            major = short.Parse(versionStrings[0]);
            minor = short.Parse(versionStrings[1]);
            subMinor = short.Parse(versionStrings[2]);
        }

        internal bool IsDifferentThan(Version _otherVersion)
        {
            if (major != _otherVersion.major)
            {
                return true;
            }
            else
            {
                if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else
                {
                    if (subMinor != _otherVersion.subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }
    }
}
