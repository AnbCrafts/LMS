using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace WebApplication1.Services
{
    public static class CloudinaryService
    {
        private static string ApiKey => ConfigurationManager.AppSettings["Cloudinary:ApiKey"] ?? "534968466568819";
        private static string ApiSecret => ConfigurationManager.AppSettings["Cloudinary:ApiSecret"] ?? "ywZbqE8MBDtLFW80a2au_R_rTf4";
        private static string CloudName => ConfigurationManager.AppSettings["Cloudinary:CloudName"] ?? "";

        /// <summary>
        /// Uploads an image file to Cloudinary
        /// . 
        /// If CloudName is not configured or an error occurs, falls back gracefully to local disk storage.
        /// </summary>
        public static string UploadImage(HttpPostedFileBase file, string subFolder = "profiles")
        {
            if (file == null || file.ContentLength == 0) return null;

            // Attempt Cloudinary upload if CloudName is configured
            if (!string.IsNullOrWhiteSpace(CloudName))
            {
                try
                {
                    string cloudUrl = UploadToCloudinary(file);
                    if (!string.IsNullOrWhiteSpace(cloudUrl))
                    {
                        return cloudUrl;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Cloudinary upload failed, falling back to local: " + ex.Message);
                }
            }

            // Local storage fallback
            return SaveLocally(file, subFolder);
        }

        private static string UploadToCloudinary(HttpPostedFileBase file)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string stringToSign = $"timestamp={timestamp}{ApiSecret}";
            string signature = GenerateSha1Hash(stringToSign);

            string endpoint = $"https://api.cloudinary.com/v1_1/{CloudName}/image/upload";

            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundaryBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            var request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.Method = "POST";
            request.KeepAlive = true;

            using (var requestStream = request.GetRequestStream())
            {
                // Form parameters
                WriteFormField(requestStream, boundaryBytes, "api_key", ApiKey);
                WriteFormField(requestStream, boundaryBytes, "timestamp", timestamp.ToString());
                WriteFormField(requestStream, boundaryBytes, "signature", signature);

                // File data
                requestStream.Write(boundaryBytes, 0, boundaryBytes.Length);
                string fileHeader = $"Content-Disposition: form-data; name=\"file\"; filename=\"{Path.GetFileName(file.FileName)}\"\r\nContent-Type: {file.ContentType}\r\n\r\n";
                byte[] fileHeaderBytes = Encoding.UTF8.GetBytes(fileHeader);
                requestStream.Write(fileHeaderBytes, 0, fileHeaderBytes.Length);

                file.InputStream.CopyTo(requestStream);

                byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                requestStream.Write(trailer, 0, trailer.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string json = reader.ReadToEnd();
                var obj = JObject.Parse(json);
                return obj["secure_url"]?.ToString() ?? obj["url"]?.ToString();
            }
        }

        private static void WriteFormField(Stream stream, byte[] boundaryBytes, string name, string value)
        {
            stream.Write(boundaryBytes, 0, boundaryBytes.Length);
            string formField = $"Content-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}";
            byte[] formFieldBytes = Encoding.UTF8.GetBytes(formField);
            stream.Write(formFieldBytes, 0, formFieldBytes.Length);
        }

        private static string GenerateSha1Hash(string input)
        {
            using (var sha1 = SHA1.Create())
            {
                byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static string SaveLocally(HttpPostedFileBase file, string subFolder)
        {
            string ext = Path.GetExtension(file.FileName);
            string fileName = Guid.NewGuid().ToString("N") + ext;
            string relativeDir = $"/Uploads/{subFolder}/";
            string serverDir = HttpContext.Current.Server.MapPath("~" + relativeDir);

            if (!Directory.Exists(serverDir))
            {
                Directory.CreateDirectory(serverDir);
            }

            string fullPath = Path.Combine(serverDir, fileName);
            file.SaveAs(fullPath);

            return relativeDir + fileName;
        }
    }
}
