using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Wnmp.UI
{
    public class SslFrm : Form
    {
        private readonly TextBox domainBox;
        private readonly CheckBox stagingCheck;
        private readonly Button requestBtn;
        private readonly Label statusLabel;

        private static string NginxDir => Path.Combine(Program.StartupPath, "nginx-bins", "default");
        private static string WacsExe => Path.Combine(Program.StartupPath, "bin", "wacs.exe");

        public SslFrm()
        {
            Text = "SSL 证书申请 (Let's Encrypt)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(460, 190);

            var lbl1 = new Label { Text = "域名 (多个用逗号分隔):", Location = new System.Drawing.Point(12, 15), AutoSize = true };
            domainBox = new TextBox { Location = new System.Drawing.Point(15, 38), Width = 430 };

            stagingCheck = new CheckBox { Text = "测试模式 (staging, 不消耗正式配额)", Location = new System.Drawing.Point(15, 68), AutoSize = true };

            requestBtn = new Button { Text = "申请证书并配置 Nginx", Location = new System.Drawing.Point(15, 100), Width = 200 };
            requestBtn.Click += RequestBtn_Click;

            statusLabel = new Label { Location = new System.Drawing.Point(15, 140), Size = new System.Drawing.Size(430, 40) };

            Controls.Add(lbl1);
            Controls.Add(domainBox);
            Controls.Add(stagingCheck);
            Controls.Add(requestBtn);
            Controls.Add(statusLabel);

            if (!File.Exists(WacsExe))
                statusLabel.Text = "未找到 wacs.exe，请将 win-acme 的 wacs.exe 放到 WNMP 的 bin 目录下。\r\n下载: https://www.win-acme.com";
        }

        private void RequestBtn_Click(object sender, EventArgs e)
        {
            string domains = domainBox.Text.Trim();
            if (domains.Length == 0) {
                MessageBox.Show(this, "请输入域名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!File.Exists(WacsExe)) {
                MessageBox.Show(this, "未找到 wacs.exe。\r\n请从 https://www.win-acme.com 下载 win-acme，\r\n把 wacs.exe 放到 " + Path.Combine(Program.StartupPath, "bin"),
                    "缺少组件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string args = $"--target manual --host {domains} --validation filesystem --webroot {Path.Combine(NginxDir, "www")} " +
                          $"--installation nginx --nginxexe \"{Path.Combine(NginxDir, "nginx.exe")}\" " +
                          $"--nginxconf \"{Path.Combine(NginxDir, "conf")}\" " +
                          $"--store pemfiles --pemfilespath \"{Path.Combine(NginxDir, "conf", "ssl")}\" " +
                          (stagingCheck.Checked ? "--baseuri https://acme-staging-v02.api.letsencrypt.org/ " : "") +
                          "--accepttos --emailaddress wnmp-admin@" + domains.Split(',')[0].Trim();

            try {
                statusLabel.Text = "正在申请证书，请稍候...（确保域名已解析到本机且 80 端口可访问）";
                Cursor = Cursors.WaitCursor;
                var psi = new ProcessStartInfo(WacsExe, args) {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(180000);

                string logFile = Path.Combine(Program.StartupPath, "ssl_request.log");
                File.WriteAllText(logFile, output);

                Cursor = Cursors.Default;
                if (p.ExitCode == 0) {
                    statusLabel.Text = "证书申请成功！已写入 nginx 配置，请重启 Nginx 生效。\r\n日志: " + logFile;
                    MessageBox.Show(this, "证书申请成功！\r\n证书目录: " + Path.Combine(NginxDir, "conf", "ssl") +
                        "\r\n请重启 Nginx 使配置生效。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                    statusLabel.Text = "申请失败，详见 ssl_request.log";
                    MessageBox.Show(this, "申请失败 (exit " + p.ExitCode + ")，详情见\r\n" + logFile,
                        "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            } catch (Exception ex) {
                Cursor = Cursors.Default;
                statusLabel.Text = "出错: " + ex.Message;
            }
        }
    }
}
