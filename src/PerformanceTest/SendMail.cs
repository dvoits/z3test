using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public class SendMail
    {
        static string domain = "@microsoft.com";
        static uint retryCount = 3;

        public static void Send(string tot, string subject, string msg, string file, Dictionary<string, string> images = null, bool html = false)
        {
            MailAddress to = new MailAddress(tot);
            string fromt = "lenka.pochernina@gmail.com";//Environment.UserName + domain;
            string password = "nordpuzzle";
            MailAddress from = new MailAddress(fromt);
            MailMessage mail = new MailMessage(from, to);
            mail.Subject = subject;
            mail.Body = msg;
            SmtpClient client = new SmtpClient("smtp.gmail.com");
            client.EnableSsl = true;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(fromt, password);
            mail.IsBodyHtml = html;

            //MailAddress to = new MailAddress("b-elpoch@microsoft.com");
            //MailAddress from = new MailAddress("lenka.pochernina@gmail.com");
            //MailMessage mail = new MailMessage(from, to); //, subject, msg);//Environment.UserName
            //mail.Subject = subject;
            //mail.Body = msg;
            //SmtpClient client = new SmtpClient("smtp.gmail.com", 587);
            //client.EnableSsl = true;
            //client.UseDefaultCredentials = false;
            //client.Credentials = new NetworkCredential("lenka.pochernina@gmail.com", "nordpuzzle");
            //mail.IsBodyHtml = html;
            if (images != null)
            {
                foreach (KeyValuePair<string, string> kvp in images)
                {
                    Uri imageUri = new Uri("pack://application:,,,/PerformanceTestManagement;component/Images/" + kvp.Value);
                    Attachment a = new Attachment(imageUri.LocalPath); 
                    a.ContentDisposition.Inline = true;
                    a.ContentDisposition.DispositionType = DispositionTypeNames.Inline;
                    a.ContentId = kvp.Key;
                    a.ContentType.MediaType = "image/" + Path.GetExtension(kvp.Value);
                    a.ContentType.Name = Path.GetFileName(kvp.Value);
                    mail.Attachments.Add(a);
                }
            }

            if (file != null)
            {
                Attachment data = new Attachment(file, MediaTypeNames.Text.Plain);
                mail.Attachments.Add(data);
            }

            uint retries = 0;
            bool sent = false;
            while (!sent)
            {
                try
                {
                    client.Send(mail);
                    sent = true;
                }
                catch (System.Net.Mail.SmtpException ex)
                {
                    retries++;
                    if (retries == retryCount)
                        throw new Exception("Failed to send email: " + ex.Message);
                }
            }
            client.Dispose();
        }
    }
}
