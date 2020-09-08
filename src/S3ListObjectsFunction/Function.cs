using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace S3ListObjectsFunction
{
    private static readonly IAmazonS3 _s3client = new AmazonS3Client();
        private static readonly AmazonSimpleNotificationServiceClient _snsClient = new AmazonSimpleNotificationServiceClient();
        private const string _bucketName = "<bucket-name>";
        private const string _topicArn = "<arn-for-sns-topic>";

    public class Function { }
    
    public async Task<string> FunctionHandler(ILambdaContext context) {
            LambdaLogger.Log($"Calling function name: {context.FunctionName}\\n");

            var result = await ListingObjectsAsync();
            await SendMessage(_snsClient, result);

            return result;
        }

        static async Task<string> ListingObjectsAsync()
        {
            var result = string.Empty;
            try
            {
                ListObjectsV2Request request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    MaxKeys = 10
                };
                ListObjectsV2Response response;
                do
                {
                    response = await _s3client.ListObjectsV2Async(request);

                    foreach (S3Object entry in response.S3Objects)
                    {
                        if (entry.Key.Contains(".csv"))
                        {
                            var status = entry.LastModified.AddHours(4.25) > DateTime.Now ? "[ 🆕 ]" : "[ 👵🏻 ]";
                            result += $"{entry.Key, -16} : {entry.LastModified.ToString("MM/dd/yyyy hh:mm tt")} {status}\n";
                        }
                        else
                        {
                            result += "\n";
                        }
                    }

                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated);
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                LambdaLogger.Log($"S3 error occurred. Exception: {amazonS3Exception.ToString()}");
            }
            catch (Exception e)
            {
                LambdaLogger.Log($"Exception: {e.ToString()}");
            }

            return result;
        }
    
        static async Task SendMessage(IAmazonSimpleNotificationService snsClient, string message)
        {
            var request = new PublishRequest
            {
                TopicArn = _topicArn,
                Message = message
            };

            await snsClient.PublishAsync(request);
        }
}
