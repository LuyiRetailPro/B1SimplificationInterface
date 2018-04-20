using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace B1SimplificationInterface
{
    public partial class Form1 : Form
    {
        MainController controller;
        public Form1(MainController controller)
        {
            InitializeComponent();
            this.controller = controller;
            loadGroupSettings(group_ADJUSTMENT);
            loadGroupSettings(group_SALE);
            loadGroupSettings(group_SLIP);
            loadGroupSettings(group_VOU_DISCREPANCY);
            loadGroupSettings(group_VOU_RETURN);
            loadGroupSettings(group_STORE_SYNC);
            loadEmailSettings();
            String[] features = Enum.GetNames(typeof(MainController.Features));
            tb_rproIPAddress.Text = controller.settings.getRproHostAddress();
            tb_B1IPAddress.Text = controller.settings.getB1HostAddress();
            tb_B1Username.Text = controller.settings.getB1Username();
            tb_B1Password.Text = controller.settings.getB1Password();
            tb_filepath.Text = controller.settings.getFilepath();
            tb_cmd.Text = controller.settings.getItemCostCMDInstruction();
            tb_ecm.Text = controller.settings.getECM();
            foreach (string feature in features)
            {
                combo_features.Items.Add(feature);
            }
            combo_features.SelectedIndex = 0;

            controller.initialize();
            string enableSSL = controller.settings.getEmailSetting(EmailController.EmailSetting.EMAIL_ENABLE_SSL);
            combo_enableSSL.SelectedIndex = combo_enableSSL.FindString(enableSSL);

            reloadLog();
        }

        private void loadEmailSettings()
        {
            Settings settings = controller.settings;
            foreach (Control control in group_email_settings.Controls)
            {
                if (control is TextBox)
                {
                    TextBox tb = control as TextBox;
                    tb.Text = settings.getEmailSetting(tb.Tag);
                    tb.LostFocus += tb_email_lostfocus;
                }
            }
        }

        private void tb_email_lostfocus(object sender, EventArgs e)
        {
            Settings settings = controller.settings;
            TextBox tb = sender as TextBox;
            settings.setEmailSetting(tb.Tag, tb.Text);
        }

        private void loadGroupSettings(GroupBox group)
        {
            Settings settings = controller.settings;
            MainController.Features feature = MainController.getFeatureByName(group.Tag.ToString());
            foreach (Control control in group.Controls)
            {
                if (control is TextBox)
                {
                    control.Tag = group.Tag;
                    if (control.Size.Width < 100)
                    {
                        TextBox tb_days = control as TextBox;
                       
                        tb_days.Text = settings.getDays(feature);
                        tb_days.LostFocus += Tb_days_LostFocus;
                    }
                    else
                    {
                        TextBox tb_subsidiaries = control as TextBox;
                        tb_subsidiaries.Text = settings.getSubsidiaries(feature);
                        tb_subsidiaries.LostFocus += Tb_subsidiaries_LostFocus;
                    }
                }
            }
        }

        private void Tb_subsidiaries_LostFocus(object sender, EventArgs e)
        {
            TextBox tb_subsidiaries = sender as TextBox;
            MainController.Features feature = MainController.getFeatureByName(tb_subsidiaries.Tag.ToString());
            string initialValue = controller.settings.getSubsidiaries(feature);
            string newValue = tb_subsidiaries.Text;
            if (string.IsNullOrWhiteSpace(newValue))
            {
                controller.settings.setSubsidiaries(feature, "");
                return;
            }
            try
            {
                string[] subsidiaries = tb_subsidiaries.Text.Split(',');
                foreach(string s in subsidiaries)
                {
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        tb_subsidiaries.Text = initialValue;
                        return;
                    }
                    int subsidiary = Int32.Parse(s);
                }
                controller.settings.setSubsidiaries(feature, tb_subsidiaries.Text);
            }catch (Exception)
            {
                tb_subsidiaries.Text = initialValue;
            }
        }

        private void Tb_days_LostFocus(object sender, EventArgs e)
        {
            TextBox tb_days = sender as TextBox;
            MainController.Features feature = MainController.getFeatureByName(tb_days.Tag.ToString());
            string initialValue = controller.settings.getDays(feature);
            string newValue = tb_days.Text;
            try
            {
                int new_days = Int32.Parse(newValue);
                controller.settings.setDays(feature, newValue);
            }catch (Exception)
            {
                tb_days.Text = initialValue;
            }
        }

        private void bn_run_Click(object sender, EventArgs e)
        {
            MainController.Features feature = MainController.getFeatureByName(combo_features.SelectedItem.ToString());
            disableForm();
            switch (feature)
            {
                case MainController.Features.ADJUSTMENT:
                    controller.runAdjustments();
                    break;
                case MainController.Features.SALE:
                    controller.runInvoices();
                    break;
                case MainController.Features.SLIP:
                    controller.runSlips();
                    break;
                case MainController.Features.VOU_DISCREPANCY:
                    controller.runVoucherDiscrepancies();
                    break;
                case MainController.Features.VOU_RETURN:
                    controller.runVoucherReturns();
                    break;
                case MainController.Features.SEND_EMAIL:
                    controller.runSendDailyEmail();
                    break;
                case MainController.Features.STORE_SYNC:
                    controller.runStoreFullSync();
                    break;
                case MainController.Features.ITEM_COST:
                    controller.runItemCost();
                    break;
            }
            reloadLog();
            enableForm();
        }

        public void disableForm()
        {
            foreach (Control control in Controls)
            {
                control.Enabled = false;
            }
        }

        public void enableForm()
        {
            foreach (Control control in Controls)
            {
                control.Enabled = true;
            }
        }

        public void reloadLog()
        {
            listview_main.Items.Clear();
            List<string[]> logs = controller.rproDBHandler.getLogDetails("");
            foreach (string[] row in logs)
            {
                ListViewItem item = new ListViewItem(row);
                listview_main.Items.Add(item);
            }
        }

        private void tb_rproIPAddress_Leave(object sender, EventArgs e)
        {
            controller.settings.setRproHostAddress(tb_rproIPAddress.Text);
        }

        private void tb_B1IPAddress_Leave(object sender, EventArgs e)
        {
            controller.settings.setB1HostAddress(tb_B1IPAddress.Text);
        }

        private void tb_B1Username_Leave(object sender, EventArgs e)
        {
            controller.settings.setB1Username(tb_B1Username.Text);
        }

        private void tb_B1Password_Leave(object sender, EventArgs e)
        {
            controller.settings.setB1Password(tb_B1Password.Text);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(tb_filepath.Text) && Directory.Exists(tb_filepath.Text))
            {
                dialog.SelectedPath = tb_filepath.Text;
            }
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                tb_filepath.Text = dialog.SelectedPath;
                controller.settings.setFilepath(tb_filepath.Text);
            }
        }

        private void tb_filepath_Leave(object sender, EventArgs e)
        {
            TextBox tb = sender as TextBox;
            string initialValue = controller.settings.getFilepath();
            if (string.IsNullOrWhiteSpace(tb.Text) || !Directory.Exists(tb.Text))
            {
                tb.Text = initialValue;
                return;
            }
            controller.settings.setFilepath(tb.Text);
        }

        private void loadErrorLogs()
        {
            listview_error.Items.Clear();
            List<string[]> logs = controller.rproDBHandler.getLogDetails("where logtype != '"+MainController.LogType.REPORT.ToString()+"'");
            foreach (string[] row in logs)
            {
                ListViewItem item = new ListViewItem(row);
                listview_error.Items.Add(item);
            }
        }

        private void loadZeroCostLogs()
        {
            listview_zerocost.Items.Clear();
            List<string[]> logs = controller.rproDBHandler.getZeroCost("");
            foreach (string[] row in logs)
            {
                ListViewItem item = new ListViewItem(row);
                listview_zerocost.Items.Add(item);
            }
            listview_zerocost.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void changetab(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab.Name == "tab_error_log")
            {
                loadErrorLogs();
            }
            else if (tabControl1.SelectedTab.Name == "tab_zero_cost_log")
            {
                loadZeroCostLogs();
            }
        }

        private void textBox11_Leave(object sender, EventArgs e)
        {
            TextBox tb = sender as TextBox;
            string daysAgo = tb.Text;
            try
            {
                Int32.Parse(daysAgo);
            }catch (Exception)
            {
                tb.Text = "15";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            controller.rproDBHandler.deleteZeroCost(tb_zero_cost.Text);
            loadZeroCostLogs();
        }

        private void bn_check_item_cost_Click(object sender, EventArgs e)
        {
            disableForm();
            try
            {
                Queue<CostDifference> costdifferences = controller.getCostDifferences();
                if (costdifferences.Count == 0)
                {
                    MessageBox.Show("No cost differences were found.");
                    return;
                }
                CostDifferenceForm form = new CostDifferenceForm(costdifferences);
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected exception occurred: " + ex.Message);
            }
            finally
            {
                enableForm();
            }

        }

        private void updateSettings(object sender, EventArgs e)
        {
            TextBox tb = sender as TextBox;
            controller.settings.setItemCostCMDInstruction(tb.Text);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "ECM Application file only (*.exe)|*.exe";
            if (!string.IsNullOrEmpty(tb_ecm.Text) && Directory.Exists(tb_ecm.Text))
            {
                dialog.FileName = tb_ecm.Text;
            }
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                tb_ecm.Text = dialog.FileName;
                controller.settings.setECM(tb_ecm.Text);
            }
        }

        private void tb_ecm_TextChanged(object sender, EventArgs e)
        {
            controller.settings.setECM((sender as TextBox).Text);
        }

     
    }
}
