﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Display.Helper.Data;
using Display.Models.Api.OneOneFive.Upload;
using Display.Models.Enums;
using Display.Models.Vo;
using HttpHeaders = Display.Constants.HttpHeaders;

namespace Display.Services.Upload.AliyunOssUpload;

/// <summary>
/// 阿里云上传接口
/// </summary>
internal abstract class BaseOssUpload : IDisposable
{
    private const string Endpoint = "http://oss-cn-shenzhen.aliyuncs.com";

    private static string Data => DateTime.UtcNow.ToString(@"ddd, dd MMM yyyy HH:mm:ss G\MT",
        CultureInfo.InvariantCulture);

    protected readonly HttpClient Client;
    protected readonly FileStream Stream;
    protected readonly Uri EndpointUri;
    protected readonly long FileSize;
    protected readonly string AccessKeyId;
    protected readonly string AccessKeySecret;
    protected readonly string CallbackBase64;
    protected readonly string CallbackVarBase64;
    protected readonly string BucketName;
    protected readonly string ObjectName;
    protected readonly string SecurityToken;
    protected readonly string BaseUrl;

    public UploadState State { get; set; }

    protected readonly CancellationToken Token;

    protected BaseOssUpload(HttpClient client, FileStream stream, OssToken ossToken, FastUploadResult fastUploadResult, CancellationToken token)
    {
        Client = client;
        Stream = stream;
        Token = token;
        FileSize = stream.Length;

        EndpointUri = new Uri(Endpoint);
        AccessKeyId = ossToken.AccessKeyId;
        AccessKeySecret = ossToken.AccessKeySecret;
        SecurityToken = ossToken.SecurityToken;

        CallbackBase64 = fastUploadResult.OssCallback.Callback;
        CallbackVarBase64 = fastUploadResult.OssCallback.CallbackVar;
        BucketName = fastUploadResult.Bucket;
        ObjectName = fastUploadResult.Object;

        BaseUrl = $"http://{BucketName}.{EndpointUri.Authority}/{ObjectName}";
    }

    protected HttpRequestMessage CreateRequest(HttpMethod method, string url, IDictionary<string, string> headers = null, HttpContent content = null)
    {
        var uri = new Uri(url);

        var request = new HttpRequestMessage
        {
            Method = method,
            RequestUri = uri,
            Content = content
        };

        headers ??= new Dictionary<string, string>();
        headers[HttpHeaders.SecurityToken] = SecurityToken;
        headers[HttpHeaders.Date] = Data;
        headers[HttpHeaders.Authorization] = OssSignHelper.GetAuthorization(AccessKeyId, AccessKeySecret, method.ToString(),
            BucketName, ObjectName, headers, OssSignHelper.GetParametersFromSignedUrl(uri));
        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }

    public abstract Task<OssUploadResult> Start();

    public abstract void Stop();

    public void Dispose()
    {
        //Cts.Cancel();
        //Cts.Dispose();

        GC.SuppressFinalize(this);
    }
}