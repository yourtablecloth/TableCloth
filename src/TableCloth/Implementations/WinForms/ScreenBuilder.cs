using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Implementations.WinForms
{
    internal static class ScreenBuilder
    {
        internal static Form CreateMainForm(IServiceProvider serviceProvider)
        {
            var form = new Form()
            {
                Text = StringResources.MainForm_Title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                Icon = new Icon(
                    new MemoryStream(Convert.FromBase64String(GraphicResources.AppIcon)),
                    64, 64),
                Size = new Size(640, 480),
                AutoScaleDimensions = new SizeF(96f, 96f),
                AutoScaleMode = AutoScaleMode.Dpi,
                Tag = serviceProvider,
            };

            var tableLayout = new TableLayoutPanel()
            {
                Parent = form,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };

            /*
             * +--------------------+
             * |                    |
             * |         A          |
             * |                    |
             * +---------+----------+
             * |    B0   |    B1    |
             * +---------+----------+
             * 
             * */

            // Row A, 90%
            _ = tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 0.9f));

            // Row B, 10%
            _ = tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 0.1f));

            // Column 0, 50%
            _ = tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0.5f));

            // Column 1, 50%
            _ = tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0.5f));

            // Row A, Column 0 + 1
            var dialogLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                AutoScroll = true,
            };
            tableLayout.SetCellPosition(dialogLayout, new TableLayoutPanelCellPosition(column: 0, row: 0));
            tableLayout.SetColumnSpan(dialogLayout, 2);

            // Row B, Column 0
            var actionLeftLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            tableLayout.SetCellPosition(actionLeftLayout, new TableLayoutPanelCellPosition(column: 0, row: 1));

            // Row B, Column 1
            var actionRightLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            tableLayout.SetCellPosition(actionRightLayout, new TableLayoutPanelCellPosition(column: 1, row: 1));

            _ = dialogLayout.CreateLabel(StringResources.MainForm_SelectOptionsLabelText);
            _ = dialogLayout.CreateLabel();

            var certListPanel = new FlowLayoutPanel()
            {
                Parent = dialogLayout,
                FlowDirection = FlowDirection.LeftToRight,
                Width = form.ClientSize.Width - 100,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            var mapNPKICert = certListPanel.CreateCheckBox(StringResources.MainForm_MapNpkiCertButtonText, true);
            var importButton = certListPanel.CreateButton(StringResources.MainForm_BrowseButtonText);
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
                using var selectForm = CreateCertSelectForm(serviceProvider);
                if (selectForm.ShowDialog(form) != DialogResult.OK)
                    return;

                if (selectForm.Tag is not X509CertPair certPair)
                    return;

                npkiFileListBox.DataSource = new string[] { certPair.DerFilePath, certPair.KeyFilePath, };
            });

            _ = importButton.DataBindings.Add(nameof(Control.Enabled), mapNPKICert, nameof(CheckBox.Checked));
            _ = npkiFileListBox.DataBindings.Add(nameof(Control.Enabled), mapNPKICert, nameof(CheckBox.Checked));

            var enableMicrophone = dialogLayout.CreateCheckBox(StringResources.MainForm_UseMicrophoneCheckboxText, false);
            var enableWebCam = dialogLayout.CreateCheckBox(StringResources.MainForm_UseWebCameraCheckboxText, false);
            var enablePrinters = dialogLayout.CreateCheckBox(StringResources.MainForm_UsePrinterCheckboxText, false);

            enableMicrophone.Font = new Font(enableMicrophone.Font, FontStyle.Bold);
            enableWebCam.Font = new Font(enableWebCam.Font, FontStyle.Bold);

            _ = dialogLayout.CreateLabel();
            var siteInstructionLabel = dialogLayout.CreateLabel(StringResources.MainForm_SelectSiteLabelText);
            _ = dialogLayout.CreateLabel();

            var siteCatalogTabControl = new TabControl
            {
                Parent = dialogLayout,
                Width = form.ClientSize.Width - 100,
                Height = 100,
                Visible = true,
            };

            var categoryValues = Enum.GetValues<CatalogInternetServiceCategory>().ToList();
            _ = categoryValues.Remove(CatalogInternetServiceCategory.Other);
            categoryValues.Add(CatalogInternetServiceCategory.Other);

            foreach (var eachCategoryType in categoryValues)
            {
                var eachTabPage = new TabPage
                {
                    Parent = siteCatalogTabControl,
                    Text = StringResources.InternetServiceCategory_DisplayText(eachCategoryType),
                    Tag = eachCategoryType,
                };

                var eachSiteCatalog = new ListBox()
                {
                    Name = "SiteList",
                    Parent = eachTabPage,
                    Dock = DockStyle.Fill,
                    SelectionMode = SelectionMode.MultiExtended,
                    IntegralHeight = false,
                };
            }

            var aboutButton = actionLeftLayout.CreateButton(StringResources.MainForm_AboutButtonText, handler: x =>
            {
                _ = MessageBox.Show(form,
                    StringResources.AboutDialog_BodyText, StringResources.TitleText_Info,
                    MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            });

            var cancelButton = actionRightLayout.CreateButton(StringResources.MainForm_CloseButtonText, handler: x =>
            {
                form.Close();
            });

            var launchButton = actionRightLayout.CreateButton(StringResources.MainForm_LaunchSandboxButtonText);

            form.CancelButton = cancelButton;
            form.AcceptButton = launchButton;

            form.Load += (_sender, _e) =>
            {
                if (_sender is not Form realSender)
                    return;

                try
                {
                    var catalogDeserializer = serviceProvider.GetService<ICatalogDeserializer>();
                    var catalog = catalogDeserializer.DeserializeCatalog(new Uri(StringResources.CatalogUrl));

                    var siteListControls = siteCatalogTabControl.TabPages.Cast<TabPage>().ToDictionary(
                        x => (CatalogInternetServiceCategory)x.Tag,
                        x => x.Controls["SiteList"] as ListBox);

                    if (catalog != null && catalog.Services.Any())
                    {
                        foreach (var eachType in catalog.Services.GroupBy(x => x.Category))
                            siteListControls[eachType.Key].DataSource = eachType.ToList();
                    }
                    else
                    {
                        siteCatalogTabControl.Visible = false;
                        siteInstructionLabel.Text = StringResources.MainForm_SelectSiteLabelText_Alt;
                    }
                }
                catch (Exception e)
                {
                    siteCatalogTabControl.Visible = false;
                    siteInstructionLabel.Text = StringResources.MainForm_SelectSiteLabelText_Alt;

                    _ = MessageBox.Show(form,
                        StringResources.Error_Cannot_Download_Catalog(e), StringResources.TitleText_Error,
                        MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }

                try
                {
                    var scanner = serviceProvider.GetService<IX509CertPairScanner>();
                    var pairs = scanner.ScanX509Pairs(scanner.GetCandidateDirectories());
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

                var scanner = serviceProvider.GetService<IX509CertPairScanner>();
                var pair = default(X509CertPair);
                var fileList = (npkiFileListBox.DataSource as IEnumerable<string>)?.ToArray();

                if (mapNPKICert.Checked && fileList != null)
                {
                    var derFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    var keyFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                    if (File.Exists(derFilePath) && File.Exists(keyFilePath))
                        pair = scanner.CreateX509CertPair(derFilePath, keyFilePath);
                }

                var activeTabPage = siteCatalogTabControl.SelectedTab;
                var activeListBox = activeTabPage.Controls["SiteList"] as ListBox;
                var activeItems = activeListBox.SelectedItems.Cast<CatalogInternetService>().ToArray();

                var config = new TableClothConfiguration()
                {
                    CertPair = pair,
                    EnableMicrophone = enableMicrophone.Checked,
                    EnableWebCam = enableWebCam.Checked,
                    EnablePrinters = enablePrinters.Checked,
                    Packages = activeItems,
                };

                var sandboxBuilder = serviceProvider.GetService<ISandboxBuilder>();
                var tempPath = sandboxBuilder.GenerateTemporaryDirectoryPath();
                var wsbFilePath = sandboxBuilder.GenerateSandboxConfiguration(tempPath, config);

                var process = new Process()
                {
                    EnableRaisingEvents = true,
                    StartInfo = new ProcessStartInfo(wsbExecPath, wsbFilePath) { UseShellExecute = false, },
                };

                process.Exited += (__sender, __e) =>
                {
                    try
                    {
                        if (__sender is Process realSender)
                        {
                            var exitCode = realSender.ExitCode;

                            if (exitCode != 0x0 && exitCode != unchecked((int)0x800700b7u))
                            {
                                _ = form.Invoke(new Action<int>((exitCode) =>
                                {
                                    _ = MessageBox.Show(form,
                                        StringResources.Error_Sandbox_ErrorCode_NonZero(exitCode), StringResources.TitleText_Error,
                                        MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);

                                }), realSender.ExitCode);
                            }
                        }

                        for (var i = 0; i < 5; i++)
                        {
                            try
                            {
                                if (Directory.Exists(tempPath))
                                    Directory.Delete(tempPath, true);
                                else
                                    break;
                            }
                            catch { Thread.Sleep(TimeSpan.FromSeconds(0.5d)); }
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = form.Invoke(new Action<Exception>((ex) =>
                        {
                            var response = MessageBox.Show(form,
                                StringResources.Error_Cannot_Remove_TempDirectory(ex), StringResources.TitleText_Error,
                                MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                            if (response != DialogResult.Yes)
                                return;

                            var explorerFilePath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                                "explorer.exe");
                            if (!File.Exists(explorerFilePath))
                            {
                                _ = MessageBox.Show(form, StringResources.Error_Windows_Explorer_Missing,
                                    StringResources.TitleText_Error, MessageBoxButtons.OK,
                                    MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                                return;
                            }

                            using var explorerProcess = new Process()
                            {
                                StartInfo = new ProcessStartInfo(explorerFilePath, tempPath),
                            };

                            if (!explorerProcess.Start())
                            {
                                _ = MessageBox.Show(form, StringResources.Error_Windows_Explorer_CanNotStart,
                                    StringResources.TitleText_Error, MessageBoxButtons.OK,
                                    MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                                return;
                            }
                        }), ex);
                    }
                };

                if (process.Start())
                    return;

                _ = form.Invoke(new Action(() =>
                {
                    _ = MessageBox.Show(form, StringResources.Error_Windows_Sandbox_CanNotStart,
                        StringResources.TitleText_Error, MessageBoxButtons.OK,
                        MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }));
            });

            return form;
        }

        internal static Form CreateCertSelectForm(IServiceProvider serviceProvider)
        {
            var form = new Form()
            {
                Text = StringResources.CertSelectForm_Title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(480, 400),
                AutoScaleDimensions = new SizeF(96f, 96f),
                AutoScaleMode = AutoScaleMode.Dpi,
                Tag = serviceProvider,
            };

            var tableLayout = new TableLayoutPanel()
            {
                Parent = form,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };

            /*
             * +--------------------+
             * |                    |
             * |         A          |
             * |                    |
             * +--------------------+
             * |         B          |
             * +--------------------+
             * 
             * */

            // Row A, Auto
            _ = tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row B, 20%
            _ = tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 0.2f));

            // Column 0, 100%
            _ = tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1f));

            // Row A
            var dialogLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                AutoScroll = true,
            };
            tableLayout.SetCellPosition(dialogLayout, new TableLayoutPanelCellPosition(column: 0, row: 0));

            // Row B
            var actionLayout = new FlowLayoutPanel()
            {
                Parent = tableLayout,
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 0, 10, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            tableLayout.SetCellPosition(actionLayout, new TableLayoutPanelCellPosition(column: 0, row: 1));

            _ = dialogLayout.CreateLabel(StringResources.CertSelectForm_InstructionLabel);
            _ = dialogLayout.CreateLabel();

            var largeListViewImageList = new ImageList()
            {
                ImageSize = new Size(48, 48),
                ColorDepth = ColorDepth.Depth32Bit,
                TransparentColor = Color.Transparent,
            };
            largeListViewImageList.Images.Add(Image.FromStream(new MemoryStream(Convert.FromBase64String(GraphicResources.CertIcon))));

            var smallListViewImageList = new ImageList()
            {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit,
                TransparentColor = Color.Transparent,
            };
            smallListViewImageList.Images.Add(Image.FromStream(new MemoryStream(Convert.FromBase64String(GraphicResources.CertIcon))));

            var certListView = new ListView()
            {
                Parent = dialogLayout,
                View = View.Tile,
                Width = form.ClientSize.Width - 100,
                LargeImageList = largeListViewImageList,
                SmallImageList = smallListViewImageList,
            };

            var refreshButton = dialogLayout.CreateButton(StringResources.CertSelectForm_RefreshButtonText);

            _ = dialogLayout.CreateLabel();
            _ = dialogLayout.CreateLabel(StringResources.CertSelectForm_ManualInstructionLabelText);
            _ = dialogLayout.CreateLabel();
            var browseCertPairButton = dialogLayout.CreateButton(StringResources.CertSelectForm_OpenNpkiCertButton);

            var cancelButton = actionLayout.CreateButton(StringResources.CancelButtonText, dialogResult: DialogResult.Cancel);
            var okayButton = actionLayout.CreateButton(StringResources.OkayButtonText, dialogResult: DialogResult.OK);

            form.CancelButton = cancelButton;
            form.AcceptButton = okayButton;
            actionLayout.Height = (int)(okayButton.Height * 1.6f);

            certListView.ItemSelectionChanged += (_sender, _e) =>
            {
                form.Tag = _e.Item.Tag as X509CertPair;
            };

            certListView.MouseDoubleClick += (_sender, _e) =>
            {
                var info = certListView.HitTest(_e.X, _e.Y);
                var item = info.Item;

                if (item != null)
                    okayButton.PerformClick();
                else
                    certListView.SelectedItems.Clear();
            };

            _ = refreshButton.AddClickEvent(x =>
              {
                  var scanner = serviceProvider.GetService<IX509CertPairScanner>();
                  var scannedPairs = scanner.ScanX509Pairs(scanner.GetCandidateDirectories());

                  if (!scannedPairs.Any())
                      return;

                  certListView.Items.Clear();

                  foreach (var eachPair in scannedPairs)
                  {
                      _ = certListView.Items.Add(new ListViewItem(eachPair.ToString())
                      {
                          Tag = eachPair,
                          ImageIndex = 0,
                      });
                  }

                  if (certListView.Items.Count > 0)
                      certListView.Items[0].Selected = true;
              });

            _ = browseCertPairButton.AddClickEvent(x =>
            {
                var scanner = serviceProvider.GetService<IX509CertPairScanner>();
                var npkiDirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "NPKI");
                if (!Directory.Exists(npkiDirectoryPath))
                    npkiDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);

                var certFileBrowserDialog = new OpenFileDialog()
                {
                    Title = StringResources.CertSelectForm_FileOpenDialog_Text,
                    Filter = StringResources.CertSelectForm_FileOpenDialog_FilterText,
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
                    _ = MessageBox.Show(form,
                        StringResources.Error_OpenDerAndKey_Simultaneously, StringResources.TitleText_Error,
                        MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    return;
                }

                form.Tag = scanner.CreateX509CertPair(derFilePath, keyFilePath);
                form.DialogResult = DialogResult.OK;
                form.Close();
            });

            form.Load += (_sender, _e) =>
            {
                refreshButton.PerformClick();
            };

            return form;
        }
    }
}
