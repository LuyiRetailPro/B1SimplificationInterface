using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace B1SimplificationInterface
{
    class EmailController
    {
        public enum EmailSetting
        {
            SMTP_ADDRESS,
            SMTP_PORT,
            EMAIL_SENDER_ADDRESS,
            EMAIL_PASSWORD,
            EMAIL_RECEPIENT_ADDRESS,
            EMAIL_ENABLE_SSL
        }
       // MainController.Features feature = MainController.Features.SEND_EMAIL;

        private string smtp_address;
        private int smtp_port;
        private string email_sender_address;
        private string email_password;
        private string email_recepient_address;
        private bool email_enable_SSL;
        private Settings settings;
        //MainController controller;

        public EmailController(Settings settings)
        {
            this.settings = settings;
            smtp_address = settings.getEmailSetting(EmailSetting.SMTP_ADDRESS);
            smtp_port = Int32.Parse(settings.getEmailSetting(EmailSetting.SMTP_PORT));
            email_sender_address = settings.getEmailSetting(EmailSetting.EMAIL_SENDER_ADDRESS);
            email_password = settings.getEmailSetting(EmailSetting.EMAIL_PASSWORD);
            email_recepient_address = settings.getEmailSetting(EmailSetting.EMAIL_RECEPIENT_ADDRESS);
            email_enable_SSL = Boolean.Parse(settings.getEmailSetting(EmailSetting.EMAIL_ENABLE_SSL));
        }

        public void sendEmail(string subject, string body, RproDBHandler rproDBHandler, MainController.Features feature)
        {
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(smtp_address, smtp_port);
                mail.From = new MailAddress(email_sender_address);
                mail.To.Add(email_recepient_address);
                mail.Subject = subject;
                mail.Body = body;

                // SmtpServer.Port = smtp_port;
                SmtpServer.UseDefaultCredentials = false;
                SmtpServer.Credentials = new System.Net.NetworkCredential(email_sender_address, email_password);
                SmtpServer.EnableSsl = email_enable_SSL;
                SmtpServer.DeliveryMethod = SmtpDeliveryMethod.Network;
                SmtpServer.Send(mail);

                rproDBHandler.addLog(MainController.LogType.REPORT, "", "", MainController.Features.SEND_EMAIL, "Email Sent", null);
            }
            catch (Exception e)
            {
                if (rproDBHandler != null)
                {
                    string msg = "Exception occurred when sending email on error report. ";
                    rproDBHandler.addLog(MainController.LogType.REPORT, "", "", feature, msg, e);
                }
            }
        }

        public void sendDailyEmail( )
        {
            RproDBHandler rproDBHandler = new RproDBHandler(settings);
            try
            {                
                List<string[]> errorLogs = rproDBHandler.getLogDetails("where logtype != '"+MainController.LogType.REPORT.ToString()+ "' and date1 > trunc(sysdate) ");
                List<string[]> zeroCostLogs = rproDBHandler.getZeroCost("where date1 > trunc(sysdate)");

                String body = "<table width='100%' cellspacing='0' border='1' style='border - collapse:collapse;' >";
                body += "<caption>Error Logs</caption>";
                foreach (var item in errorLogs)
                {
                    body += "<tr>";
                    for(int i=0; i<item.Length-1;i++)
                    {
                        body += "<td stlye='color:blue;'>" + item[i] + "</td>";
                    }
                    body += "</tr>";
                }
                body += "</table>";
                body += "<br> <br/>";

                body += "<table width='100%' cellspacing='0' border='1' style='border - collapse:collapse;' >";
                body += "<caption>Zero Cost Logs</caption>";
                foreach (var item in zeroCostLogs)
                {
                    body += "<tr>";
                    for (int i = 0; i < item.Length; i++)
                    {
                        body += "<td stlye='color:blue;'>" + item[i] + "</td>";
                    }
                    body += "</tr>";
                }
                body += "</table>";

                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(smtp_address, smtp_port);
                mail.From = new MailAddress(email_sender_address);
                mail.To.Add(email_recepient_address);
                mail.Subject = "B1 Daily Email " + System.DateTime.Today.ToShortDateString().ToString();
                mail.IsBodyHtml = true;
                mail.Body = body;

                //SmtpServer.Port = smtp_port;
                SmtpServer.Credentials = new System.Net.NetworkCredential(email_sender_address, email_password);
                SmtpServer.EnableSsl = email_enable_SSL;
                SmtpServer.DeliveryMethod = SmtpDeliveryMethod.Network;
                SmtpServer.Send(mail);

                rproDBHandler.addLog(MainController.LogType.REPORT, "", "", MainController.Features.SEND_EMAIL, "Email Sent (Today's Log)", null);
            }
            catch (Exception e)
            {
                if (rproDBHandler != null)
                {
                    string msg = "Exception occurred when sending email on error report. ";
                    rproDBHandler.addLog(MainController.LogType.REPORT, "", "", MainController.Features.SEND_EMAIL, msg, e);
                }
            }
        }
     
    }
}
