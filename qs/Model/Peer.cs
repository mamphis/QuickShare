namespace qs.Model
{
    using System;
    using System.Net;

    [Serializable]
    internal class Peer
    {
        public string Code { get; set; }
        public int TcpPort { get; set; }
        public string IP { get; set; }

        public IPEndPoint IPEndPoint => new IPEndPoint(IPAddress.Parse(this.IP), this.TcpPort);
    }
}
