namespace UDPChatServer.Datamodels
{
    internal class ClientState
    {
        public string Username { get; set; } = "";
        public DateTime LastSeen;
        public DateTime LastPingTime;
        public DateTime LastServerPing;
        public DateTime JoinTime { get; set; }
        public ClientStatus clientStatus;
    }

    enum ClientStatus
    {
        Offline,
        Available,
        Busy
    }
}
