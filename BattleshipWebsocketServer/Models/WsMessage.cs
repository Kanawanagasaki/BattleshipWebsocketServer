using Newtonsoft.Json.Linq;

namespace BattleshipWebsocketServer.Models;

public class WsMessage
{
    public MessageType Type { get; set; }
    public string? Method { get; set; }
    public JToken? Args { get; set; }
    public string? Comment { get; set; } = null;
    public string? Raw { get; set; } = null;

    public WsMessage(MessageType type, string? method, JToken? args = null)
        => (Type, Method, Args) = (type, method?.ToLower(), args);

    public WsMessage Response(object? args = null, string? comment = null)
        => new WsMessage(MessageType.Response, Method, args is null ? null : JObject.FromObject(args)) { Comment = comment };

    public WsMessage Response(bool success, string? message, object? args = null, string? comment = null)
    {
        var obj = args is null ? new JObject() : JObject.FromObject(args);
        obj[nameof(success)] = success;
        if (!string.IsNullOrWhiteSpace(message))
            obj[nameof(message)] = message;
        return new WsMessage(MessageType.Response, Method, obj) { Comment = comment };
    }

    public enum MessageType
    {
        Welcome, Request, Event, Response, NotAuthorised, Error
    }
}
