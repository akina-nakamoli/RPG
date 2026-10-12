using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>HTTPS account/save client. It never starts or stops backend processes.</summary>
public sealed class DatabaseAccountApi
{
    [Serializable] public class Config
    {
        public bool enabled;
        public string apiUrl;
    }
    [Serializable] private class Request
    {
        public string operation, username, password, token, accountId, invitationCode;
        public int slot, revision;
        public string requestId;
        public GameSaveData save;
    }
    // These fields are populated by Unity's JSON deserializer.
#pragma warning disable 0649
    [Serializable] private class Response
    {
        public bool ok, exists;
        public string error, token, lastRequestId;
        public int revision;
        public LocalAccount account;
        public GameSaveData save;
    }
#pragma warning restore 0649
    private readonly string url;
    private string token;
    public DatabaseAccountApi(string endpoint)
    {
        Uri parsed = new Uri(endpoint);
        if (parsed.Scheme != "https" && !(parsed.IsLoopback && parsed.Scheme == "http"))
            throw new InvalidOperationException("Cloud API requires HTTPS; HTTP is allowed only on localhost.");
        url = endpoint;
    }
    public async Task<LocalAccount> AuthenticateAsync(string username, string password, bool register, string invitationCode = null)
    {
        Response result = await Send(new Request { operation = register ? "register" : "login", username = username,
            password = password, invitationCode = register ? invitationCode : null });
        token = result.token;
        return result.account;
    }
    public async Task<bool> HasSaveAsync(string id, int slot) => (await Send(new Request { operation = "has", accountId = id, slot = slot })).exists;
    public async Task DeleteSaveAsync(string id, int slot, int revision) => await Send(new Request { operation = "delete", accountId = id, slot = slot, revision = revision });
    public async Task<GameSaveData> LoadAsync(string id, int slot)
    {
        Response response = await Send(new Request { operation = "load", accountId = id, slot = slot });
        response.save.cloudRevision = response.revision;
        response.save.cloudPending = false;
        response.save.cloudConflict = false;
        response.save.cloudRequestId = response.lastRequestId;
        return response.save;
    }
    public async Task<int> SaveAsync(GameSaveData save) => (await Send(new Request { operation = "save",
        accountId = save.accountId, slot = save.slot, save = save, revision = save.cloudRevision,
        requestId = save.cloudRequestId })).revision;
    public async Task LogoutAsync(string accountId)
    {
        try { if (!string.IsNullOrEmpty(token)) await Send(new Request { operation = "logout", accountId = accountId }); }
        finally { token = null; }
    }
    public class ApiException : Exception
    {
        public long Status { get; private set; }
        public ApiException(string message, long status) : base(message) { Status = status; }
    }

    private async Task<Response> Send(Request input)
    {
        input.token = token;
        byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(input));
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "POST";
        request.ContentType = "application/json";
        request.Timeout = 5000;
        request.ReadWriteTimeout = 5000;
        request.Proxy = null;
        request.ContentLength = payload.Length;
        using (var deadline = new System.Threading.Timer(_ => request.Abort(), null, 10000, System.Threading.Timeout.Infinite))
        {
        try
        {
            using (Stream stream = await request.GetRequestStreamAsync()) await stream.WriteAsync(payload, 0, payload.Length);
            using (WebResponse response = await request.GetResponseAsync()) return await Read(response);
        }
        catch (WebException ex)
        {
            if (ex.Response != null) using (WebResponse response = ex.Response) return await Read(response);
            throw new ApiException("Game server is offline or unreachable. Ask the host to start it manually, then retry. Local progress is preserved.", 0);
        }
        }
    }
    private static async Task<Response> Read(WebResponse response)
    {
        using (var reader = new StreamReader(response.GetResponseStream()))
        {
            Response result = JsonUtility.FromJson<Response>(await reader.ReadToEndAsync());
            if (result == null || !result.ok) throw new ApiException(result != null ? result.error : "Invalid API response.",
                response is HttpWebResponse http ? (long)http.StatusCode : 0);
            return result;
        }
    }
}

