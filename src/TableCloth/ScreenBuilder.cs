using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using TableCloth.Models;

namespace TableCloth
{
    internal static class ScreenBuilder
    {
		public static Form CreateMainForm()
		{
			var form = new Form()
			{
				Text = "식탁보 - 컴퓨터를 깨끗하게 사용하세요!",
				Size = new Size(640, 400),
				MinimumSize = new Size(640, 400),
				Icon = new Icon(
					new MemoryStream(Convert.FromBase64String(GraphicResources.AppIcon)),
					64, 64),
			};

			var dialogLayout = new FlowLayoutPanel()
			{
				Parent = form,
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.TopDown,
				Padding = new Padding(16),
			};

			var actionLayout = new FlowLayoutPanel()
			{
				Parent = form,
				Dock = DockStyle.Bottom,
				FlowDirection = FlowDirection.RightToLeft,
			};

			CreateLabel(dialogLayout, "원하는 옵션을 선택해주세요.");
			CreateLabel(dialogLayout);

			var certListPanel = new FlowLayoutPanel()
			{
				Parent = dialogLayout,
				FlowDirection = FlowDirection.LeftToRight,
				Width = form.ClientSize.Width - 100,
			};
			var mapNPKICert = CreateCheckBox(certListPanel, "기존 공인인증서 파일 가져오기(&C)", true);
			var importButton = CreateButton(certListPanel, "찾아보기(&B)...");
			certListPanel.Height = (int)(mapNPKICert.Height * 1.6f);

			var npkiFileListBox = new ListBox()
			{
				Parent = dialogLayout,
				Height = 60,
				IntegralHeight = true,
				Width = form.ClientSize.Width - 100,
			};

            _ = importButton.AddClickEvent(x =>
              {
                  using var selectForm = CreateCertSelectForm();
                  if (selectForm.ShowDialog(form) != DialogResult.OK)
                      return;

                  if (selectForm.Tag is not X509CertPair certPair)
                      return;

                  npkiFileListBox.DataSource = new string[] { certPair.DerFilePath, certPair.KeyFilePath, };
              });

            _ = importButton.DataBindings.Add(nameof(Control.Enabled), mapNPKICert, nameof(CheckBox.Checked));
            _ = npkiFileListBox.DataBindings.Add(nameof(Control.Enabled), mapNPKICert, nameof(CheckBox.Checked));

			var enableMicrophone = CreateCheckBox(dialogLayout, "오디오 입력 사용하기(&A) - 개인 정보 노출에 주의하세요!", false);
			var enableWebCam = CreateCheckBox(dialogLayout, "비디오 입력 사용하기(&V) - 개인 정보 노출에 주의하세요!", false);
			var enablePrinters = CreateCheckBox(dialogLayout, "프린터 같이 사용하기(&P)", true);

			enableMicrophone.Font = new Font(enableMicrophone.Font, FontStyle.Bold);
			enableWebCam.Font = new Font(enableWebCam.Font, FontStyle.Bold);

			CreateLabel(dialogLayout);
			var siteInstructionLabel = CreateLabel(dialogLayout, "식탁보 위에서 접속할 사이트를 선택해주세요. 사이트에서 필요한 프로그램들을 자동으로 설치해드려요.");
			CreateLabel(dialogLayout);

			var selectedSiteComboBox = new ComboBox()
			{
				Parent = dialogLayout,
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = form.ClientSize.Width - 100,
			};

			var cancelButton = CreateButton(actionLayout, "닫기", handler: x =>
			{
				form.Close();
			});

			var launchButton = CreateButton(actionLayout, "샌드박스 실행");

			form.CancelButton = cancelButton;
			form.AcceptButton = launchButton;
			actionLayout.Height = (int)(launchButton.Height * 1.6f);

			form.Load += (_sender, _e) =>
			{
                if (_sender is not Form realSender)
                    return;

                bool is64BitOperatingSystem = (IntPtr.Size == 8) || NativeMethods.InternalCheckIsWow64();

				if (!is64BitOperatingSystem)
				{
                    _ = MessageBox.Show("실행하고 있는 운영 체제는 윈도우 샌드박스 기능을 지원하지 않는 오래된 버전의 운영 체제 같습니다. 윈도우 10 이상으로 업그레이드 해주세요.",
                        "프로그램을 실행할 수 없습니다.", MessageBoxButtons.OK, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button1);
					realSender.Close();
					return;
				}

				var wsbExecPath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.System),
					"WindowsSandbox.exe");

				if (!File.Exists(wsbExecPath))
				{
                    _ = MessageBox.Show("윈도우 샌드박스가 설치되어있지 않은 것 같습니다! 프로그램 추가/제거 - Windows 기능 켜기/끄기에서 설정해주세요.",
                        "프로그램을 실행할 수 없습니다.", MessageBoxButtons.OK, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button1);
					realSender.Close();
					return;
				}

				using var webClient = new WebClient();
				var catalogFilePath = Path.Combine(Path.GetTempPath(), "Catalog.txt");

				try { webClient.DownloadFile("https://dotnetdev-kr.github.io/TableCloth/Catalog.txt", catalogFilePath); }
				catch { }

				var catalog = CatalogBuilder.ParseCatalog(catalogFilePath);

				if (catalog != null && catalog.Any())
					selectedSiteComboBox.DataSource = catalog;
				else
					siteInstructionLabel.Text = "카탈로그 파일을 가져오지 못했어요! 그래도 샌드박스는 대신 실행해드려요.";

				try
				{
					var pairs = X509CertPair.ScanX509CertPairs();
					if (pairs != null && pairs.Count() == 1)
                    {
						var pair = pairs.First();
						npkiFileListBox.DataSource = new string[] { pair.DerFilePath, pair.KeyFilePath, };
                    }
				}
				catch { }
			};

            _ = launchButton.AddClickEvent(x =>
              {
                  var wsbExecPath = Path.Combine(
                      Environment.GetFolderPath(Environment.SpecialFolder.System),
                      "WindowsSandbox.exe");

                  var pair = default(X509CertPair);
                  var fileList = (npkiFileListBox.DataSource as IEnumerable<string>)?.ToArray();

                  if (mapNPKICert.Checked && fileList != null)
                  {
                      var derFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                      var keyFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                      if (File.Exists(derFilePath) && File.Exists(keyFilePath))
                          pair = X509CertPair.CreateX509CertPair(derFilePath, keyFilePath);
                  }

                  SandboxConfiguration config = new()
                  {
                      CertPair = pair,
                      EnableMicrophone = enableMicrophone.Checked,
                      EnableWebCam = enableWebCam.Checked,
                      EnablePrinters = enablePrinters.Checked,
                      SelectedService = selectedSiteComboBox.SelectedItem as InternetService,
                  };

                  var tempDirectoryName = "bwsb_" + Guid.NewGuid().ToString("n");
                  var tempPath = Path.Combine(Path.GetTempPath(), tempDirectoryName);
                  var wsbFilePath = SandboxBuilder.GenerateSandboxConfiguration(tempPath, config);

                  var psi = new ProcessStartInfo(wsbExecPath, wsbFilePath) { UseShellExecute = false, };
                  _ = Process.Start(psi);
              });

			return form;
		}

		static Form CreateCertSelectForm()
        {
			var form = new Form()
			{
				Text = "검색된 공인 인증서 선택",
				Size = new Size(640, 360),
				MinimumSize = new Size(320, 200),
				FormBorderStyle = FormBorderStyle.FixedDialog,
				ShowInTaskbar = false,
				MinimizeBox = false,
				MaximizeBox = false,
				StartPosition = FormStartPosition.CenterParent,
			};

			var dialogLayout = new FlowLayoutPanel()
			{
				Parent = form,
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.TopDown,
				Padding = new Padding(16),
			};

			var actionLayout = new FlowLayoutPanel()
			{
				Parent = form,
				Dock = DockStyle.Bottom,
				FlowDirection = FlowDirection.RightToLeft,
			};

			CreateLabel(dialogLayout, "검색된 공인 인증서가 다음과 같습니다. 다음 중 하나를 선택해주세요.");
			CreateLabel(dialogLayout);

			var listView = new ListView()
			{
				Parent = dialogLayout,
				View = View.Tile,
				Width = form.ClientSize.Width - 100,
			};

			var refreshButton = CreateButton(dialogLayout, "새로 고침(&R)");
			
			CreateLabel(dialogLayout);
			CreateLabel(dialogLayout, "원하는 인증서가 없다면, 직접 인증서 찾기 버튼을 눌러서 직접 DER 파일과 KEY 파일을 찾아주세요.");
			CreateLabel(dialogLayout);
			var browseCertPairButton = CreateButton(dialogLayout, "직접 인증서 찾기(&B)...");

			var cancelButton = CreateButton(actionLayout, "취소", dialogResult: DialogResult.Cancel);
			var okayButton = CreateButton(actionLayout, "확인", dialogResult: DialogResult.OK);

			form.CancelButton = cancelButton;
			form.AcceptButton = okayButton;
			actionLayout.Height = (int)(okayButton.Height * 1.6f);

			listView.ItemSelectionChanged += (_sender, _e) =>
			{
				form.Tag = _e.Item.Tag as X509CertPair;
			};

			listView.MouseDoubleClick += (_sender, _e) =>
			{
				ListViewHitTestInfo info = listView.HitTest(_e.X, _e.Y);
				ListViewItem item = info.Item;

				if (item != null)
					okayButton.PerformClick();
				else
					listView.SelectedItems.Clear();
			};

            _ = refreshButton.AddClickEvent(x =>
              {
                  var scannedPairs = X509CertPair.ScanX509CertPairs();

                  if (!scannedPairs.Any())
                      return;

                  listView.Items.Clear();

                  foreach (var eachPair in scannedPairs)
                  {
                      _ = listView.Items.Add(new ListViewItem(eachPair.ToString())
                      {
                          Tag = eachPair,
                      });
                  }

                  if (listView.Items.Count > 0)
                      listView.Items[0].Selected = true;
              });

            _ = browseCertPairButton.AddClickEvent(x =>
              {
                  var npkiDirectoryPath = Path.Combine(
                          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                          "AppData", "LocalLow", "NPKI");
                  if (!Directory.Exists(npkiDirectoryPath))
                      npkiDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);

                  var certFileBrowserDialog = new OpenFileDialog()
                  {
                      Title = "인증서 파일 (signCert.der, signPri.key) 열기",
                      Filter = "인증서 파일 (*.der;*.key)|*.der;*.key|모든 파일|*.*",
                      ReadOnlyChecked = true,
                      SupportMultiDottedExtensions = true,
                      DereferenceLinks = true,
                      Multiselect = true,
                      InitialDirectory = npkiDirectoryPath,
                  };

                  if (certFileBrowserDialog.ShowDialog(form) != DialogResult.OK)
                      return;

                  var selectedFiles = certFileBrowserDialog.FileNames;
                  var derFilePath = selectedFiles.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                  var keyFilePath = selectedFiles.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                  if (!File.Exists(derFilePath) || !File.Exists(keyFilePath))
                  {
                      _ = MessageBox.Show(form, "인증서 정보 파일 (der)과 개인 키 파일 (key)을 각각 하나씩 선택해주세요.\r\n\r\nCtrl 키나 Shift 키를 누른 채로 선택하거나, 파일 선택 창에서 빈 공간을 드래그하면 여러 파일을 선택할 수 있어요.",
                          "오류", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                      return;
                  }

                  form.Tag = X509CertPair.CreateX509CertPair(derFilePath, keyFilePath);
                  form.DialogResult = DialogResult.OK;
                  form.Close();
              });

			form.Load += (_sender, _e) =>
			{
				refreshButton.PerformClick();
			};

			return form;
		}

		static Label CreateLabel(Control parentControl, string text = default)
		{
			var label = new Label()
			{
				Parent = parentControl,
				Text = text ?? string.Empty,
				AutoSize = true,
			};

			return label;
		}

		public static CheckBox CreateCheckBox(Control parentControl, string text, bool @checked = false)
		{
			var checkBox = new CheckBox()
			{
				Parent = parentControl,
				Text = text,
				Checked = @checked,
				AutoSize = true,
				TextAlign = ContentAlignment.MiddleLeft,
			};

			return checkBox;
		}

		public static Button CreateButton(Control parentControl, string text, DialogResult dialogResult = default, Action<Button> handler = null)
		{
			var button = new Button()
			{
				Parent = parentControl,
				Text = text,
				AutoSize = true,
				DialogResult = dialogResult,
			};

			if (handler != null)
				button = button.AddClickEvent(handler);

			return button;
		}

		public static TButtonBase AddClickEvent<TButtonBase>(this TButtonBase targetControl, Action<TButtonBase> handler)
			where TButtonBase : ButtonBase
        {
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			targetControl.Click += new EventHandler((_sender, _e) =>
			{
                if (_sender is TButtonBase realSender && handler != null)
                    handler.Invoke(realSender);
            });

			return targetControl;
		}
	}
}
