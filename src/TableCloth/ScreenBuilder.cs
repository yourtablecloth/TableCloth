using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TableCloth.Models;

namespace TableCloth
{
	internal static class ScreenBuilder
    {
		public static Form CreateMainForm()
		{
			var wsbExecPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.System),
				"WindowsSandbox.exe");

			var catalogFilePath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
				"Catalog.txt");

			var catalog = CatalogBuilder.ParseCatalog(catalogFilePath);

			var form = new Form()
			{
				Text = "식탁보 - 컴퓨터를 깨끗하게 사용하세요!",
				Size = new Size(640, 320),
				MinimumSize = new Size(640, 320),
			};

			form.Load += (_sender, _e) =>
			{
				var realSender = _sender as Form;
				if (realSender == null)
					return;

				bool is64BitOperatingSystem = (IntPtr.Size == 8) || NativeMethods.InternalCheckIsWow64();

				if (!is64BitOperatingSystem)
				{
					MessageBox.Show("실행하고 있는 운영 체제는 윈도우 샌드박스 기능을 지원하지 않는 오래된 버전의 운영 체제 같습니다. 윈도우 10 이상으로 업그레이드 해주세요.",
						"프로그램을 실행할 수 없습니다.", MessageBoxButtons.OK, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button1);
					realSender.Close();
					return;
				}

				if (!File.Exists(wsbExecPath))
				{
					MessageBox.Show("윈도우 샌드박스가 설치되어있지 않은 것 같습니다! 프로그램 추가/제거 - Windows 기능 켜기/끄기에서 설정해주세요.",
						"프로그램을 실행할 수 없습니다.", MessageBoxButtons.OK, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button1);
					realSender.Close();
					return;
				}
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
			var mapNPKICert = CreateCheckBox(dialogLayout, "컴퓨터 공인인증서 가져오기(&C)", true);
			var selectedSiteComboBox = default(ComboBox);

			if (catalog.Count() > 0)
            {
				CreateLabel(dialogLayout);
				CreateLabel(dialogLayout, "식탁보 위에서 접속할 사이트를 선택해주세요. 사이트에서 필요한 프로그램들을 자동으로 설치해드려요.");
				CreateLabel(dialogLayout);

				selectedSiteComboBox = new ComboBox()
				{
					Parent = dialogLayout,
					DropDownStyle = ComboBoxStyle.DropDownList,
					DataSource = catalog,
					Width = form.ClientSize.Width - 100,
				};
			}
			else
            {
				CreateLabel(dialogLayout);
				CreateLabel(dialogLayout, "카탈로그 파일을 가져오지 못했어요! 그래도 샌드박스는 대신 실행해드려요.");
				CreateLabel(dialogLayout);
			}

			var cancelButton = CreateButton(actionLayout, "닫기", x =>
			{
				form.Close();
			});
			form.CancelButton = cancelButton;

			var launchButton = CreateButton(actionLayout, "샌드박스 실행", x =>
			{
				var config = new SandboxConfiguration()
				{
					MapNPKICert = mapNPKICert.Checked,
					SelectedService = selectedSiteComboBox.SelectedItem as InternetService,
				};

				var tempDirectoryName = "bwsb_" + Guid.NewGuid().ToString("n");
				var tempPath = Path.Combine(Path.GetTempPath(), tempDirectoryName);
				var wsbFilePath = SandboxBuilder.GenerateSandboxConfiguration(tempPath, config);

				var psi = new ProcessStartInfo(wsbExecPath, wsbFilePath) { UseShellExecute = false, };
				Process.Start(psi);
			});
			launchButton.Width += 50;

			form.AcceptButton = launchButton;

			actionLayout.Height = (int)(launchButton.Height * 1.6f);
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

		public static Button CreateButton(Control parentControl, string text, Action<Button> handler = null)
		{
			var button = new Button()
			{
				Parent = parentControl,
				Text = text,
			};

			if (handler != null)
			{
				button.Click += new EventHandler((_sender, _e) =>
				{
					Button realSender = _sender as Button;
					if (realSender != null && handler != null)
						handler.Invoke(realSender);
				});
			}

			return button;
		}
	}
}
