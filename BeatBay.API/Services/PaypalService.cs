using BeatBay.Model;
using Microsoft.Extensions.Configuration;
using PayPal;
using PayPal.Api;
using System;
using System.Collections.Generic;

namespace BeatBay.Services
{
    public class PayPalService
    {
        private readonly IConfiguration _configuration;

        public PayPalService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private APIContext GetAPIContext()
        {
            try
            {
                var config = new Dictionary<string, string>
                {
                    { "mode", _configuration["PayPal:Mode"] ?? "sandbox" },
                    { "clientId", _configuration["PayPal:ClientId"] },
                    { "clientSecret", _configuration["PayPal:ClientSecret"] }
                };

                // Validar que las credenciales no sean nulas
                if (string.IsNullOrEmpty(config["clientId"]) || string.IsNullOrEmpty(config["clientSecret"]))
                {
                    throw new InvalidOperationException("PayPal credentials are missing from configuration");
                }

                var accessToken = new OAuthTokenCredential(
                    config["clientId"],
                    config["clientSecret"],
                    config).GetAccessToken();

                return new APIContext(accessToken) { Config = config };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create PayPal API context: {ex.Message}", ex);
            }
        }

        public PayPal.Api.Payment CreatePayment(string planName, decimal amount, int planId, int userId, string baseUrl)
        {
            try
            {
                var apiContext = GetAPIContext();

                // Validar parámetros
                if (string.IsNullOrEmpty(planName))
                    throw new ArgumentException("Plan name cannot be null or empty");

                if (amount <= 0)
                    throw new ArgumentException("Amount must be greater than zero");

                if (string.IsNullOrEmpty(baseUrl))
                    throw new ArgumentException("Base URL cannot be null or empty");

                // Formatear el monto correctamente
                var formattedAmount = Math.Round(amount, 2).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                var payment = new PayPal.Api.Payment
                {
                    intent = "sale",
                    payer = new Payer
                    {
                        payment_method = "paypal"
                    },
                    transactions = new List<Transaction>
                    {
                        new Transaction
                        {
                            description = $"Suscripción al plan {planName}",
                            invoice_number = $"PLAN_{planId}_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                            amount = new Amount
                            {
                                currency = "USD",
                                total = formattedAmount
                            },
                            item_list = new ItemList
                            {
                                items = new List<PayPal.Api.Item>
                                {
                                    new PayPal.Api.Item
                                    {
                                        name = $"Plan {planName}",
                                        currency = "USD",
                                        price = formattedAmount,
                                        quantity = "1",
                                        sku = $"PLAN_{planId}"
                                    }
                                }
                            },
                            custom = $"{planId}|{userId}" // Para identificar el plan y usuario
                        }
                    },
                    redirect_urls = new RedirectUrls
                    {
                        return_url = $"{baseUrl}/api/PlanSimulation/execute-payment",
                        cancel_url = $"{baseUrl}/api/PlanSimulation/cancel-payment"
                    }
                };

                var createdPayment = payment.Create(apiContext);
                return createdPayment;
            }
            catch (PayPalException ex)
            {
                // Log del error específico de PayPal
                throw new InvalidOperationException($"PayPal API Error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error creating PayPal payment: {ex.Message}", ex);
            }
        }

        public PayPal.Api.Payment ExecutePayment(string paymentId, string payerId)
        {
            try
            {
                if (string.IsNullOrEmpty(paymentId))
                    throw new ArgumentException("Payment ID cannot be null or empty");

                if (string.IsNullOrEmpty(payerId))
                    throw new ArgumentException("Payer ID cannot be null or empty");

                var apiContext = GetAPIContext();
                var paymentExecution = new PaymentExecution { payer_id = payerId };
                var payment = new PayPal.Api.Payment { id = paymentId };

                return payment.Execute(apiContext, paymentExecution);
            }
            catch (PayPalException ex)
            {
                throw new InvalidOperationException($"PayPal Execution Error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error executing PayPal payment: {ex.Message}", ex);
            }
        }

        public PayPal.Api.Payment GetPayment(string paymentId)
        {
            try
            {
                if (string.IsNullOrEmpty(paymentId))
                    throw new ArgumentException("Payment ID cannot be null or empty");

                var apiContext = GetAPIContext();
                return PayPal.Api.Payment.Get(apiContext, paymentId);
            }
            catch (PayPalException ex)
            {
                throw new InvalidOperationException($"PayPal Get Payment Error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error getting PayPal payment: {ex.Message}", ex);
            }
        }
    }
}