using Microsoft.AspNetCore.Mvc;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using System.Net;
using System.Net.Mail;

namespace TrackingApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public SessionController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("OracleDbConnection");
        }

        // 1. Send OTP to Email
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] OtpRequestDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email))
                return BadRequest(new { success = false, message = "Email is required." });

            string generatedOtp = new Random().Next(100000, 999999).ToString();

            using (var db = new OracleConnection(_connectionString))
            {
                var existingUser = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM app_users WHERE email = :Email", new { Email = request.Email });

                if (existingUser == 0)
                {
                    await db.ExecuteAsync(
                        "INSERT INTO app_users (full_name, email, otp_code, is_verified) VALUES (:FullName, :Email, :OtpCode, 0)",
                        new { FullName = "Pending User", Email = request.Email, OtpCode = generatedOtp });
                }
                else
                {
                    await db.ExecuteAsync(
                        "UPDATE app_users SET otp_code = :OtpCode WHERE email = :Email",
                        new { OtpCode = generatedOtp, Email = request.Email });
                }
            }

            try
            {
                var emailSettings = _configuration.GetSection("EmailSettings");
                var smtpClient = new SmtpClient(emailSettings["Server"])
                {
                    Port = int.Parse(emailSettings["Port"]),
                    Credentials = new NetworkCredential(emailSettings["SenderEmail"], emailSettings["Password"]),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(emailSettings["SenderEmail"], emailSettings["SenderName"]),
                    Subject = "Your Safety Tracker Verification Code",
                    Body = $"Your verification code is: <b>{generatedOtp}</b>",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(request.Email);

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to send email: " + ex.Message });
            }

            return Ok(new { success = true, message = "OTP sent successfully to email." });
        }

        // 2. Verify OTP & Complete Registration
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.OtpCode))
                return BadRequest(new { success = false, message = "Email and OTP are required." });

            using (var db = new OracleConnection(_connectionString))
            {
                var storedOtp = await db.ExecuteScalarAsync<string>(
                    "SELECT otp_code FROM app_users WHERE email = :Email", new { Email = request.Email });

                if (storedOtp == null || storedOtp != request.OtpCode)
                    return BadRequest(new { success = false, message = "Invalid OTP code." });

                await db.ExecuteAsync(
                    "UPDATE app_users SET is_verified = 1 WHERE email = :Email",
                    new { Email = request.Email });
            }

            return Ok(new { success = true, message = "OTP verified successfully. Access granted!" });
        }
    }

    public class OtpRequestDto
    {
        public string Email { get; set; }
    }

    public class VerifyOtpDto
    {
        public string Email { get; set; }
        public string OtpCode { get; set; }
    }
}