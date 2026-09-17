using Microsoft.AspNetCore.Mvc;
using Dapper;
using Npgsql;
using System.Net;
using System.Net.Mail;

namespace TrackingApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string? _connectionString;

        public SessionController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("postgrelsqlconn")
                                ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        }

        // 1. Send OTP to Email
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] OtpRequestDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email))
                return BadRequest(new { success = false, message = "Email is required." });

            if (string.IsNullOrEmpty(_connectionString))
                return StatusCode(500, new { success = false, message = "Database connection string is missing." });

            string generatedOtp = new Random().Next(100000, 999999).ToString();

            try
            {
                using (var db = new NpgsqlConnection(_connectionString))
                {
                    await db.OpenAsync();

                    // Table auto-create ensure karne ke liye
                    await db.ExecuteAsync(@"
                        CREATE TABLE IF NOT EXISTS app_users (
                            id SERIAL PRIMARY KEY,
                            full_name VARCHAR(100),
                            email VARCHAR(150) UNIQUE,
                            otp_code VARCHAR(10),
                            is_verified INT DEFAULT 0
                        );");

                    var existingUser = await db.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM app_users WHERE email = @Email", new { Email = request.Email });

                    if (existingUser == 0)
                    {
                        await db.ExecuteAsync(
                            "INSERT INTO app_users (full_name, email, otp_code, is_verified) VALUES (@FullName, @Email, @OtpCode, 0)",
                            new { FullName = "Pending User", Email = request.Email, OtpCode = generatedOtp });
                    }
                    else
                    {
                        await db.ExecuteAsync(
                            "UPDATE app_users SET otp_code = @OtpCode WHERE email = @Email",
                            new { OtpCode = generatedOtp, Email = request.Email });
                    }
                }
            }
            catch (Exception ex)
            {
                // ✅ Full stack trace return hoga
                return StatusCode(500, new { success = false, message = "Database Error: " + ex.ToString() });
            }

            try
            {
                var emailSettings = _configuration.GetSection("EmailSettings");
                string server = emailSettings["Server"] ?? "smtp.gmail.com";
                int port = int.TryParse(emailSettings["Port"], out var p) ? p : 587;
                string senderEmail = emailSettings["SenderEmail"] ?? "";
                string senderName = emailSettings["SenderName"] ?? "Aura Tracker";
                string password = emailSettings["Password"] ?? "";

                var smtpClient = new SmtpClient(server)
                {
                    Port = port,
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = "Your Safety Tracker Verification Code",
                    Body = $"Your verification code is: <b>{generatedOtp}</b>",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(request.Email);

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                // ✅ Full stack trace return hoga
                return StatusCode(500, new { success = false, message = "Failed to send email: " + ex.ToString() });
            }

            return Ok(new { success = true, message = "OTP sent successfully to email." });
        }

        // 2. Verify OTP & Complete Registration
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.OtpCode))
                return BadRequest(new { success = false, message = "Email and OTP are required." });

            if (string.IsNullOrEmpty(_connectionString))
                return StatusCode(500, new { success = false, message = "Database connection string is missing." });

            try
            {
                using (var db = new NpgsqlConnection(_connectionString))
                {
                    await db.OpenAsync();

                    var storedOtp = await db.ExecuteScalarAsync<string>(
                        "SELECT otp_code FROM app_users WHERE email = @Email", new { Email = request.Email });

                    if (storedOtp == null || storedOtp != request.OtpCode)
                        return BadRequest(new { success = false, message = "Invalid OTP code." });

                    await db.ExecuteAsync(
                        "UPDATE app_users SET is_verified = 1 WHERE email = @Email",
                        new { Email = request.Email });
                }
            }
            catch (Exception ex)
            {
                // ✅ Full stack trace return hoga
                return StatusCode(500, new { success = false, message = "Database Error: " + ex.ToString() });
            }

            return Ok(new { success = true, message = "OTP verified successfully. Access granted!" });
        }
    }

    public class OtpRequestDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyOtpDto
    {
        public string Email { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
    }
}