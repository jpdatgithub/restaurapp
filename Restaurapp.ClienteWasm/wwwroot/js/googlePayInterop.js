window.googlePayInterop = (function () {
    let paymentsClient = null;
    let currentEnvironment = null;

    function buildBaseRequest() {
        return {
            apiVersion: 2,
            apiVersionMinor: 0
        };
    }

    function buildBaseCardPaymentMethod() {
        return {
            type: "CARD",
            parameters: {
                allowedAuthMethods: ["PAN_ONLY", "CRYPTOGRAM_3DS"],
                allowedCardNetworks: ["AMEX", "DISCOVER", "INTERAC", "JCB", "MASTERCARD", "VISA"]
            }
        };
    }

    function buildCardPaymentMethod(options) {
        return {
            ...buildBaseCardPaymentMethod(),
            tokenizationSpecification: {
                type: "PAYMENT_GATEWAY",
                parameters: {
                    gateway: options.gateway || "example",
                    gatewayMerchantId: options.gatewayMerchantId || "exampleGatewayMerchantId"
                }
            }
        };
    }

    function buildPaymentDataRequest(options) {
        const request = buildBaseRequest();
        request.allowedPaymentMethods = [buildCardPaymentMethod(options)];
        request.transactionInfo = {
            totalPriceStatus: "FINAL",
            totalPrice: options.totalPrice || "0.01",
            currencyCode: options.currencyCode || "BRL",
            countryCode: options.countryCode || "BR"
        };
        request.merchantInfo = {
            merchantName: options.merchantName || "Restaurapp Teste"
        };

        if (options.merchantId) {
            request.merchantInfo.merchantId = options.merchantId;
        }

        return request;
    }

    async function waitForGooglePay(timeoutMs) {
        const startedAt = Date.now();

        while (Date.now() - startedAt < timeoutMs) {
            if (window.google && window.google.payments && window.google.payments.api) {
                return window.google.payments.api;
            }

            await new Promise(resolve => window.setTimeout(resolve, 100));
        }

        throw new Error("O script do Google Pay não foi carregado.");
    }

    async function getPaymentsClient(environment) {
        await waitForGooglePay(10000);

        const normalizedEnvironment = (environment || "TEST").toUpperCase();
        if (!paymentsClient || currentEnvironment !== normalizedEnvironment) {
            paymentsClient = new google.payments.api.PaymentsClient({
                environment: normalizedEnvironment
            });
            currentEnvironment = normalizedEnvironment;
        }

        return paymentsClient;
    }

    async function requestPayment(options, dotNetRef) {
        try {
            const client = await getPaymentsClient(options.environment);
            const paymentDataRequest = buildPaymentDataRequest(options);
            const paymentData = await client.loadPaymentData(paymentDataRequest);
            const token = paymentData && paymentData.paymentMethodData && paymentData.paymentMethodData.tokenizationData
                ? paymentData.paymentMethodData.tokenizationData.token
                : null;

            if (!token) {
                throw new Error("O Google Pay não retornou um token de pagamento.");
            }

            if (dotNetRef) {
                await dotNetRef.invokeMethodAsync("OnGooglePayAuthorized", token);
            }
        } catch (error) {
            const message = error && error.statusCode === "CANCELED"
                ? "Pagamento cancelado pelo usuário."
                : (error && error.message ? error.message : "Falha ao iniciar o Google Pay.");

            if (dotNetRef) {
                await dotNetRef.invokeMethodAsync("OnGooglePayError", message);
            }
        }
    }

    async function renderButton(containerId, options, dotNetRef) {
        const container = document.getElementById(containerId);
        if (!container) {
            throw new Error("Container do botão Google Pay não encontrado.");
        }

        container.innerHTML = "";

        const client = await getPaymentsClient(options.environment);
        const button = client.createButton({
            onClick: function () { requestPayment(options, dotNetRef); },
            buttonType: "pay",
            buttonColor: options.buttonColor || "black",
            buttonRadius: 6
        });

        container.appendChild(button);

        try {
            const readyToPayRequest = buildBaseRequest();
            readyToPayRequest.allowedPaymentMethods = [buildBaseCardPaymentMethod()];
            const ready = await client.isReadyToPay(readyToPayRequest);

            if (!ready || !ready.result) {
                if (dotNetRef) {
                    await dotNetRef.invokeMethodAsync("OnGooglePayStatusChanged", "Google Pay indisponível neste navegador ou perfil.");
                }
            }
        } catch {
            if (dotNetRef) {
                await dotNetRef.invokeMethodAsync("OnGooglePayStatusChanged", "Não foi possível validar a disponibilidade do Google Pay.");
            }
        }
    }

    function clear(containerId) {
        const container = document.getElementById(containerId);
        if (container) {
            container.innerHTML = "";
        }
    }

    return {
        renderButton: renderButton,
        clear: clear
    };
})();