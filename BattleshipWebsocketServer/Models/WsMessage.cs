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

    public WsMessage Response(JToken? args = null, string? comment = null)
        => new WsMessage(MessageType.Response, Method, args) { Comment = comment, Raw = Raw };

    public enum MessageType
    {
        Welcome, Request, Response, NotAuthorised, Error
    }
}
