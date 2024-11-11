
using Legacy.Database;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System;
using System.IO;
using System.Net;
using System.Text;
using Unity.Collections;

public static class IAPValidator
{
    static string validateDomain = "http://ringrage-iap-check.islandsville.com/api";
    //static public string fakeReceipt = "{\"Store\":\"GooglePlay\",\"TransactionID\":\"gnkpheohgnjheopdmdfkigoo.AO-J1OwhifwE6nYar0Qb7GtHeMciAa2TMqi4XuePOTp0vJN1XFNznBIUeeu522w80J5sFrhwNn4DtbRC9x1iankRWp08yHfwbA\",\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"GPA.3339-4600-5295-42104\\\\\\\",\\\\\\\"packageName\\\\\\\":\\\\\\\"com.enixan.ringrage\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"ringrage.gems_80_0.99\\\\\\\",\\\\\\\"purchaseTime\\\\\\\":1609235808043,\\\\\\\"purchaseState\\\\\\\":0,\\\\\\\"purchaseToken\\\\\\\":\\\\\\\"gnkpheohgnjheopdmdfkigoo.AO-J1OwhifwE6nYar0Qb7GtHeMciAa2TMqi4XuePOTp0vJN1XFNznBIUeeu522w80J5sFrhwNn4DtbRC9x1iankRWp08yHfwbA\\\\\\\",\\\\\\\"acknowledged\\\\\\\":false}\\\",\\\"signature\\\":\\\"Fx1zRvqhqqEx+IcGQzAIZUzuws9WX5wC+620ik6KJrg5knJpVlQzgl46ywTe0t1Pxdgqe6cqz9eEWnuQiqU09+sOwA0a/enqKieC3OPGd4i+2IWemY4zg7RXD8D+sWBmjImCaZmX/3q4pdUJhMYKibNXKpnGU3OUFhSiPF2sE/DedZWcdZvT3OTIdoXgDdRkZBLM0Ri1YnVtThZcG4cwx2yqwcuYXxMgZ/+Twl8bOVIRtdQBVfolXJl6tCJzmXPyxlJ8MJX0Oi/3FeUjF2QAj0w75OBG8oc5NiNR6n1rzbKLEOAX2uFpI3p/0d59q+TN1VLpPayViavC+53ejCb6wA==\\\",\\\"skuDetails\\\":\\\"{\\\\\\\"productId\\\\\\\":\\\\\\\"ringrage.gems_80_0.99\\\\\\\",\\\\\\\"type\\\\\\\":\\\\\\\"inapp\\\\\\\",\\\\\\\"price\\\\\\\":\\\\\\\"27,99\\\\u00a0\\\\u0433\\\\u0440\\\\u043d.\\\\\\\",\\\\\\\"price_amount_micros\\\\\\\":2799990000,\\\\\\\"price_currency_code\\\\\\\":\\\\\\\"UAH\\\\\\\",\\\\\\\"title\\\\\\\":\\\\\\\"Fistful of Gems (Ring Rage)\\\\\\\",\\\\\\\"description\\\\\\\":\\\\\\\"Receive 80 gems\\\\\\\",\\\\\\\"skuDetailsToken\\\\\\\":\\\\\\\"AEuhp4If4MHoO945VfNQ7uVa7KkDnhXx2yAZvgeFgTz47s6C4-h5EZcsypTUd7nT6FYb\\\\\\\"}\\\"}\"}";
    //static public string fakeReceipt = "{\"Store\":\"GooglePlay\",\"TransactionID\":\"ehnkmgpafhcpoblpbdoclncp.AO-J1OwAnK48yAMFN2DW8GLi8pUKT8fxIZkyKS04vaKpMUeU6rtLd73h-UVkIQ2t7sFaIHV3AzpMFv49BA232ot6Qrtq1eOMhQ\",\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"GPA.3372-9488-9600-98195\\\\\\\",\\\\\\\"packageName\\\\\\\":\\\\\\\"com.enixan.ringrage\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"regems6k49\\\\\\\",\\\\\\\"purchaseTime\\\\\\\":1613383253734,\\\\\\\"purchaseState\\\\\\\":0,\\\\\\\"purchaseToken\\\\\\\":\\\\\\\"ehnkmgpafhcpoblpbdoclncp.AO-J1OwAnK48yAMFN2DW8GLi8pUKT8fxIZkyKS04vaKpMUeU6rtLd73h-UVkIQ2t7sFaIHV3AzpMFv49BA232ot6Qrtq1eOMhQ\\\\\\\",\\\\\\\"acknowledged\\\\\\\":false}\\\",\\\"signature\\\":\\\"GtJiSV74yAz893jLq6VbBE9hKQ6qWEtLrrBWC4Npk7DVss+NApn60MbnJ3/oHU6wbLsa3TlNNrpiaMyUKazMI89qGGuNns3T8yN8v2W4iBmwWfraTv/L8i7iNnLbv2wbFGj9iNNNhaNab6qgwQzMLGlAFgR/zsTNsvfRUaAM+Cfpb/F5tn5VdOhQ+bynMvA2L9XP1n/Z8RrJp16NemsKVahRD4s6e4Bik8QgI6bQSRz9Fu/QpuP3fdGHZl3ugDr2481u8MS72imQO3bQou3uwFS3JMvd2SrdWC+sOWQmHDH/7EqPuZxO6x3YEtSFZYztVSoxkcOX1GoDu6c2D9OKVg==\\\",\\\"skuDetails\\\":\\\"{\\\\\\\"productId\\\\\\\":\\\\\\\"regems6k49\\\\\\\",\\\\\\\"type\\\\\\\":\\\\\\\"inapp\\\\\\\",\\\\\\\"price\\\\\\\":\\\\\\\"1\\\\u00a0399,99\\\\u00a0\\\\u0433\\\\u0440\\\\u043d.\\\\\\\",\\\\\\\"price_amount_micros\\\\\\\":1399990000,\\\\\\\"price_currency_code\\\\\\\":\\\\\\\"UAH\\\\\\\",\\\\\\\"title\\\\\\\":\\\\\\\"Box of Gems (Ring Rage)\\\\\\\",\\\\\\\"description\\\\\\\":\\\\\\\"Box of gems (Ring Rage)\\\\\\\",\\\\\\\"skuDetailsToken\\\\\\\":\\\\\\\"AEuhp4LoEp8bD7HYwzNucC9dlTHZd4bfAXB-4bD7h4jNzTbHJL1n090SzUF9cfrtZGg=\\\\\\\"}\\\"}\"}";
    static public string fakeReceipt = "{\"Store\":\"GooglePlay\",\"TransactionID\":\"ehnkmgpafhcpoblpbdoclncp.AO-J1OwAnK48yAMFN2DW8GLi8pUKT8fxIZkyKS04vaKpMUeU6rtLd73h-UVkIQ2t7sFaIHV3AzpMFv49BA232ot6Qrtq1eOMhQ\",\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"GPA.3372-9488-9600-98195\\\\\\\",\\\\\\\"packageName\\\\\\\":\\\\\\\"com.enixan.ringrage\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"regems6k49\\\\\\\",\\\\\\\"purchaseTime\\\\\\\":1613383253734,\\\\\\\"purchaseState\\\\\\\":0,\\\\\\\"purchaseToken\\\\\\\":\\\\\\\"ehnkmgpafhcpoblpbdoclncp.AO-J1OwAnK48yAMFN2DW8GLi8pUKT8fxIZkyKS04vaKpMUeU6rtLd73h-UVkIQ2t7sFaIHV3AzpMFv49BA232ot6Qrtq1eOMhQ\\\\\\\",\\\\\\\"acknowledged\\\\\\\":false}\\\",\\\"signature\\\":\\\"GtJiSV74yAz893jLq6VbBE9hKQ6qWEtLrrBWC4Npk7DVss+NApn60MbnJ3/oHU6wbLsa3TlNNrpiaMyUKazMI89qGGuNns3T8yN8v2W4iBmwWfraTv/L8i7iNnLbv2wbFGj9iNNNhaNab6qgwQzMLGlAFgR/zsTNsvfRUaAM+Cfpb/F5tn5VdOhQ+bynMvA2L9XP1n/Z8RrJp16NemsKVahRD4s6e4Bik8QgI6bQSRz9Fu/QpuP3fdGHZl3ugDr2481u8MS72imQO3bQou3uwFS3JMvd2SrdWC+sOWQmHDH/7EqPuZxO6x3YEtSFZYztVSoxkcOX1GoDu6c2D9OKVg==\\\",\\\"skuDetails\\\":\\\"{\\\\\\\"productId\\\\\\\":\\\\\\\"regems6k49\\\\\\\",\\\\\\\"type\\\\\\\":\\\\\\\"inapp\\\\\\\",\\\\\\\"price\\\\\\\":\\\\\\\"1\\\\u00a0399,99\\\\u00a0\\\\u0433\\\\u0440\\\\u043d.\\\\\\\",\\\\\\\"price_amount_micros\\\\\\\":2799990000,\\\\\\\"price_currency_code\\\\\\\":\\\\\\\"UAH\\\\\\\",\\\\\\\"title\\\\\\\":\\\\\\\"Box of Gems (Ring Rage)\\\\\\\",\\\\\\\"description\\\\\\\":\\\\\\\"Box of gems (Ring Rage)\\\\\\\",\\\\\\\"skuDetailsToken\\\\\\\":\\\\\\\"AEuhp4LoEp8bD7HYwzNucC9dlTHZd4bfAXB-4bD7h4jNzTbHJL1n090SzUF9cfrtZGg=\\\\\\\"}\\\"}\"}";
    //static public string fakeReceipt = "{\"Store\":\"GooglePlay\",\"TransactionID\":\"gmmhfjbpffpjmnogelnfcdgi.AO-J1OwsRYbmJ-H3sSbOERWUQV9dRs76CS_-_aph4pd_j7cmkKDekdMKrPGA7yoWcs9gTBfVcJcVctkymhL5RYBiGHEjuLkK_w\",\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"GPA.3369-4217-9510-38205\\\\\\\",\\\\\\\"packageName\\\\\\\":\\\\\\\"com.enixan.ringrage\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"ringrage.gems_80_0.99\\\\\\\",\\\\\\\"purchaseTime\\\\\\\":1608896042218,\\\\\\\"purchaseState\\\\\\\":0,\\\\\\\"purchaseToken\\\\\\\":\\\\\\\"gmmhfjbpffpjmnogelnfcdgi.AO-J1OwsRYbmJ-H3sSbOERWUQV9dRs76CS_-_aph4pd_j7cmkKDekdMKrPGA7yoWcs9gTBfVcJcVctkymhL5RYBiGHEjuLkK_w\\\\\\\",\\\\\\\"acknowledged\\\\\\\":false}\\\",\\\"signature\\\":\\\"j81jBhLhxY69QC9ua+cQPWSHVtzDXBjYwfeqO+Xen/b4zf02KXnaTsFwDiwlHf/c1maX1kYYMORrGDEPC5yyzy7468rU3lSQ0Oy/SoNFYPGhFc1cCQNzkzjChDtCs3Ko2aXd+KICwToZdCDwzkqQPI5NAK2oO4kNDwIixzr91l++b0zimL2owCp5bzV41Qy0YW65R92UuCHAvaRZZQEWcAaF6c0rhvpl9WftE2O+9FHJk9s8B06iLUuR9D3aULBusrKpP7KY0ScouRR2WH/faUScrRuVO39UmWmZcNI1XvJppSxfowlq4Q7Ab0FnVu3koO47F6POG0v8wlmZ2hrMGw==\\\",\\\"skuDetails\\\":\\\"{\\\\\\\"productId\\\\\\\":\\\\\\\"ringrage.gems_80_0.99\\\\\\\",\\\\\\\"type\\\\\\\":\\\\\\\"inapp\\\\\\\",\\\\\\\"price\\\\\\\":\\\\\\\"27,99\\\\u00a0\\\\u0433\\\\u0440\\\\u043d.\\\\\\\",\\\\\\\"price_amount_micros\\\\\\\":27990000,\\\\\\\"price_currency_code\\\\\\\":\\\\\\\"UAH\\\\\\\",\\\\\\\"title\\\\\\\":\\\\\\\"Fistful of Gems (Ring Rage)\\\\\\\",\\\\\\\"description\\\\\\\":\\\\\\\"Receive 80 gems\\\\\\\",\\\\\\\"skuDetailsToken\\\\\\\":\\\\\\\"AEuhp4K9MhHn8xO3z3J2lFXFDbM1wua5H7eEODGcoH_TLWTp7U3oMh8MHS3geUsyTsja\\\\\\\"}\\\"}\"}";

    public static void FakeValidate(out ObserverPlayerPaymentResult paymentResult)
    {
        ValidateReceipt(fakeReceipt, out ObserverPlayerPaymentResult _paymentResult);
        paymentResult = _paymentResult;
    }

    public static bool ValidateReceipt(FixedString4096 receipt, out ObserverPlayerPaymentResult paymentResult)
    {
        paymentResult = default;
        UnityEngine.Debug.Log($"Try Validate. Receipt: {receipt}");

        try
        {
            paymentResult.receiptHash = receipt.GetHashCode();
            BsonDocument BsonReceipt = BsonSerializer.Deserialize<BsonDocument>(receipt.ToString());
            BsonDocument Payload = BsonSerializer.Deserialize<BsonDocument>(BsonReceipt.GetValue("Payload").AsString);
            BsonDocument JsonData = BsonSerializer.Deserialize<BsonDocument>(Payload.GetValue("json").AsString);
            BsonDocument skuDetails = BsonSerializer.Deserialize<BsonDocument>(Payload.GetValue("skuDetails").AsString);

            UnityEngine.Debug.Log($"Start validating skuDetails: {skuDetails}");
            UnityEngine.Debug.Log($"Start validating JsonData: {JsonData}");
            UnityEngine.Debug.Log($"Start validating Payload: {Payload}");
            UnityEngine.Debug.Log($"Start validating receipt: {receipt}");
            bool validReceipt = false;
            var request = (HttpWebRequest)WebRequest.Create(validateDomain);

            request.Method = "POST";
            request.ContentType = "application/json";
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(receipt.ToString());
            }

            var response = (HttpWebResponse)request.GetResponse();
            Stream responseStream = response.GetResponseStream();
            StreamReader streamReader = new StreamReader(responseStream);
            var responseString = streamReader.ReadToEnd();

            //var responseString = "{\"status\":true,\"info\":{\"orderId\":\"GPA.3339 - 4600 - 5295 - 42104\",\"purchaseTime\":1609235808043,\"service\":\"google\"}}";
            UnityEngine.Debug.Log($"responseString from validating server: {responseString}");

            BsonDocument result = BsonSerializer.Deserialize<BsonDocument>(responseString);
            UnityEngine.Debug.Log($"BSON result: {result}");

            if (result.TryGetValue("status", out BsonValue status))
            {
                UnityEngine.Debug.Log($"Purchase status: {status}");

                if (status.AsBoolean)
                {
                    BsonDocument info = result.GetValue("info").AsBsonDocument;
                    UnityEngine.Debug.Log($"Valid purchase Info: {info}");

                    string orderId = info.GetValue("orderId").AsString;
                    //if (JsonData.GetValue("orderId").AsString == orderId) {
                    validReceipt = true;
                    UnityEngine.Debug.Log($"Valid purchase: product - {JsonData.GetValue("productId").AsString}");
                    paymentResult.payment.orderID = orderId;
                    paymentResult.payment.productID = JsonData.GetValue("productId").AsString;
                    paymentResult.payment.transactionID = BsonReceipt.GetValue("TransactionID").AsString;
                    BsonValue bson_price = skuDetails.GetValue("price_amount_micros");
                    long priceValue = bson_price.IsInt32 ? (long)bson_price.AsInt32 : bson_price.AsInt64;
                    paymentResult.payment.price = (float)(priceValue / 1000000.0f);
                    paymentResult.payment.ISOCurrencyCode = skuDetails.GetValue("price_currency_code").AsString;
                    paymentResult.payment.title = skuDetails.GetValue("title").AsString;
                    paymentResult.payment.purchaseTime = new DateTime(JsonData.GetValue("purchaseTime").AsInt64);

                    UnityEngine.Debug.Log($"paymentResult.payment: {paymentResult.payment}");

                    //}
                }
                else
                {
                    UnityEngine.Debug.Log($"Purchase Error Message: {result.GetValue("msg").AsString}");
                    UnityEngine.Debug.Log($"Purchase Error status: {result.GetValue("status").AsBoolean}");
                }                
            }
            else
            {
                
            }
            paymentResult.success = validReceipt;
            return validReceipt;
        }
        catch(Exception e)
        {
            UnityEngine.Debug.LogError($"Validating crashes. Exception - {e.Message}. Trace: - {e.StackTrace}. Receipt: {receipt}");

            return true;
        }
    }
}

