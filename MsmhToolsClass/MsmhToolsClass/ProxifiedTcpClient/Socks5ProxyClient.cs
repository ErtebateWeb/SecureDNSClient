//
//  Author:
//       Benton Stark <benton.stark@gmail.com>
//
//  Copyright (c) 2016 Benton Stark
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Diagnostics;

namespace MsmhToolsClass.ProxifiedTcpClient;

/// <summary>
/// Socks5 connection proxy class. This class implements the Socks5 standard proxy protocol.
/// </summary>
/// <remarks>
/// This implementation supports TCP proxy connections with a Socks v5 server.
/// </remarks>
public class Socks5ProxyClient
{
    private string _proxyHost = IPAddress.Loopback.ToString(); // Default
    private int _proxyPort = 1080; // Default
    private string _proxyUsername = string.Empty;
    private string _proxyPassword = string.Empty;
    private SocksAuthentication? _proxyAuthMethod;
    private TcpClient? _tcpClient;
    private TcpClient? _tcpClientCached;

    private const string PROXY_NAME = "SOCKS5";

    private const byte SOCKS5_VERSION_NUMBER = 5;
    private const byte SOCKS5_RESERVED = 0x00;
    private const byte SOCKS5_AUTH_NUMBER_OF_AUTH_METHODS_SUPPORTED = 2;
    private const byte SOCKS5_AUTH_METHOD_NO_AUTHENTICATION_REQUIRED = 0x00;
    private const byte SOCKS5_AUTH_METHOD_GSSAPI = 0x01;
    private const byte SOCKS5_AUTH_METHOD_USERNAME_PASSWORD = 0x02;
    private const byte SOCKS5_AUTH_METHOD_IANA_ASSIGNED_RANGE_BEGIN = 0x03;
    private const byte SOCKS5_AUTH_METHOD_IANA_ASSIGNED_RANGE_END = 0x7f;
    private const byte SOCKS5_AUTH_METHOD_RESERVED_RANGE_BEGIN = 0x80;
    private const byte SOCKS5_AUTH_METHOD_RESERVED_RANGE_END = 0xfe;
    private const byte SOCKS5_AUTH_METHOD_REPLY_NO_ACCEPTABLE_METHODS = 0xff;
    private const byte SOCKS5_CMD_CONNECT = 0x01;
    private const byte SOCKS5_CMD_BIND = 0x02;
    private const byte SOCKS5_CMD_UDP_ASSOCIATE = 0x03;
    private const byte SOCKS5_CMD_REPLY_SUCCEEDED = 0x00;
    private const byte SOCKS5_CMD_REPLY_GENERAL_SOCKS_SERVER_FAILURE = 0x01;
    private const byte SOCKS5_CMD_REPLY_CONNECTION_NOT_ALLOWED_BY_RULESET = 0x02;
    private const byte SOCKS5_CMD_REPLY_NETWORK_UNREACHABLE = 0x03;
    private const byte SOCKS5_CMD_REPLY_HOST_UNREACHABLE = 0x04;
    private const byte SOCKS5_CMD_REPLY_CONNECTION_REFUSED = 0x05;
    private const byte SOCKS5_CMD_REPLY_TTL_EXPIRED = 0x06;
    private const byte SOCKS5_CMD_REPLY_COMMAND_NOT_SUPPORTED = 0x07;
    private const byte SOCKS5_CMD_REPLY_ADDRESS_TYPE_NOT_SUPPORTED = 0x08;
    private const byte SOCKS5_ADDRTYPE_IPV4 = 0x01;
    private const byte SOCKS5_ADDRTYPE_DOMAIN_NAME = 0x03;
    private const byte SOCKS5_ADDRTYPE_IPV6 = 0x04;

    /// <summary>
    /// Authentication itemType.
    /// </summary>
    private enum SocksAuthentication
    {
        None,
        UsernamePassword
    }

    /// <summary>
    /// Create a Socks5 proxy client object. 
    /// </summary>
    public Socks5ProxyClient()
    {

    }

    /// <summary>
    /// Creates a Socks5 proxy client object using the supplied TcpClient object connection.
    /// </summary>
    /// <param name="tcpClient">A TcpClient connection object.</param>
    public Socks5ProxyClient(TcpClient tcpClient)
    {
        _tcpClientCached = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
    }

    /// <summary>
    /// Create a Socks5 proxy client object.
    /// </summary>
    /// <param name="proxyHost">Host name or IP address of the proxy server.</param>
    /// <param name="proxyPort">Port used to connect to proxy server.</param>
    public Socks5ProxyClient(string proxyHost, int proxyPort)
    {
        if (proxyPort <= 0 || proxyPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(proxyPort), "port must be greater than zero and less than 65535");

        _proxyHost = proxyHost ?? throw new ArgumentNullException(nameof(proxyHost));
        _proxyPort = proxyPort;
    }

    /// <summary>
    /// Create a Socks5 proxy client object.  
    /// </summary>
    /// <param name="proxyHost">Host name or IP address of the proxy server.</param>
    /// <param name="proxyPort">Port used to connect to proxy server.</param>
    /// <param name="proxyUsername">Proxy authentication user name.</param>
    /// <param name="proxyPassword">Proxy authentication password.</param>
    public Socks5ProxyClient(string proxyHost, int proxyPort, string? proxyUsername, string? proxyPassword)
    {
        if (proxyPort <= 0 || proxyPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(proxyPort), "port must be greater than zero and less than 65535");
        _proxyHost = proxyHost ?? throw new ArgumentNullException(nameof(proxyHost));
        _proxyPort = proxyPort;

        if (!string.IsNullOrEmpty(proxyUsername))
            _proxyUsername = proxyUsername;

        if (!string.IsNullOrEmpty(proxyPassword))
            _proxyPassword = proxyPassword;
    }

    /// <summary>
    /// Gets or sets host name or IP address of the proxy server.
    /// </summary>
    public string ProxyHost
    {
        get { return _proxyHost; }
        set { _proxyHost = value; }
    }

    /// <summary>
    /// Gets or sets port used to connect to proxy server.
    /// </summary>
    public int ProxyPort
    {
        get { return _proxyPort; }
        set { _proxyPort = value; }
    }

    /// <summary>
    /// Gets String representing the name of the proxy. 
    /// </summary>
    /// <remarks>This property will always return the value 'SOCKS5'</remarks>
    public string ProxyName
    {
        get { return PROXY_NAME; }
    }

    /// <summary>
    /// Gets or sets the TcpClient object. 
    /// This property can be set prior to executing CreateConnection to use an existing TcpClient connection.
    /// </summary>
    public TcpClient TcpClient
    {
        //get { return _tcpClientCached; }
        set { _tcpClientCached = value; }
    }

    /// <summary>
    /// Creates a remote TCP connection through a proxy server to the destination host on the destination port.
    /// </summary>
    /// <param name="destinationHost">Destination host name or IP address of the destination server.</param>
    /// <param name="destinationPort">Port number to connect to on the destination host.</param>
    /// <returns>
    /// Returns an open TcpClient object that can be used normally to communicate
    /// with the destination server
    /// </returns>
    public async Task<TcpClient?> CreateConnection(string destinationHost, int destinationPort)
    {
        if (string.IsNullOrEmpty(destinationHost))
            throw new ArgumentNullException(nameof(destinationHost));

        if (destinationPort <= 0 || destinationPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(destinationPort), "port must be greater than zero and less than 65535");

        try
        {
            // if we have no cached tcpip connection then create one
            if (_tcpClientCached == null)
            {
                if (string.IsNullOrEmpty(_proxyHost))
                    throw new Exception("ProxyHost property must contain a value.");

                if (_proxyPort <= 0 || _proxyPort > 65535)
                    throw new Exception("ProxyPort value must be greater than zero and less than 65535");

                //  create new tcp client object to the proxy server
                _tcpClient = new TcpClient();

                // attempt to open the connection
                await _tcpClient.ConnectAsync(_proxyHost, _proxyPort);
            }
            else
            {
                _tcpClient = _tcpClientCached;
            }

            // determine which authentication method the client would like to use
            DetermineClientAuthMethod();

            // negotiate which authentication methods are supported / accepted by the server
            await NegotiateServerAuthMethod();

            // send a connect command to the proxy server for destination host and port
            await SendCommand(SOCKS5_CMD_CONNECT, destinationHost, destinationPort);

            // remove the private reference to the tcp client so the proxy object does not keep it
            // return the open proxied tcp client object to the caller for normal use
            TcpClient rtn = _tcpClient;
            _tcpClient = null;
            return rtn;
        }
        catch (Exception ex)
        {
            string msg;
            if (_tcpClient == null)
                msg = "Tcp Client is null.";
            else
                msg = $"Connection to proxy {Utils.GetHost(_tcpClient)}:{Utils.GetPort(_tcpClient)} failed.";
            Debug.WriteLine($"{msg}{Environment.NewLine}{ex.Message}");

            return null;
            //throw new ProxyException(msg, ex);
        }
    }

    private void DetermineClientAuthMethod()
    {
        //  set the authentication itemType used based on values inputed by the user
        if (!string.IsNullOrEmpty(_proxyUsername) && !string.IsNullOrEmpty(_proxyPassword))
            _proxyAuthMethod = SocksAuthentication.UsernamePassword;
        else
            _proxyAuthMethod = SocksAuthentication.None;
    }

    private async Task NegotiateServerAuthMethod()
    {
        if (_tcpClient == null) return;

        //  get a reference to the network stream
        NetworkStream stream = _tcpClient.GetStream();

        // SERVER AUTHENTICATION REQUEST
        // The client connects to the server, and sends a version
        // identifier/method selection message:
        //
        //      +----+----------+----------+
        //      |VER | NMETHODS | METHODS  |
        //      +----+----------+----------+
        //      | 1  |    1     | 1 to 255 |
        //      +----+----------+----------+

        byte[] authRequest = new byte[4];
        authRequest[0] = SOCKS5_VERSION_NUMBER;
        authRequest[1] = SOCKS5_AUTH_NUMBER_OF_AUTH_METHODS_SUPPORTED;
        authRequest[2] = SOCKS5_AUTH_METHOD_NO_AUTHENTICATION_REQUIRED;
        authRequest[3] = SOCKS5_AUTH_METHOD_USERNAME_PASSWORD;

        //  send the request to the server specifying authentication types supported by the client.
        await stream.WriteAsync(authRequest);

        //  SERVER AUTHENTICATION RESPONSE
        //  The server selects from one of the methods given in METHODS, and
        //  sends a METHOD selection message:
        //
        //     +----+--------+
        //     |VER | METHOD |
        //     +----+--------+
        //     | 1  |   1    |
        //     +----+--------+
        //
        //  If the selected METHOD is X'FF', none of the methods listed by the
        //  client are acceptable, and the client MUST close the connection.
        //
        //  The values currently defined for METHOD are:
        //   * X'00' NO AUTHENTICATION REQUIRED
        //   * X'01' GSSAPI
        //   * X'02' USERNAME/PASSWORD
        //   * X'03' to X'7F' IANA ASSIGNED
        //   * X'80' to X'FE' RESERVED FOR PRIVATE METHODS
        //   * X'FF' NO ACCEPTABLE METHODS

        //  receive the server response 
        byte[] response = new byte[2];
        await stream.ReadAsync(response);

        //  the first byte contains the socks version number (e.g. 5)
        //  the second byte contains the auth method acceptable to the proxy server
        byte acceptedAuthMethod = response[1];

        // if the server does not accept any of our supported authenication methods then throw an error
        if (acceptedAuthMethod == SOCKS5_AUTH_METHOD_REPLY_NO_ACCEPTABLE_METHODS)
        {
            _tcpClient.Close();
            string msg = "The proxy destination does not accept the supported proxy client authentication methods.";
            Debug.WriteLine(msg);
            return;
            //throw new ProxyException(msg);
        }

        // if the server accepts a username and password authentication and none is provided by the user then throw an error
        if (acceptedAuthMethod == SOCKS5_AUTH_METHOD_USERNAME_PASSWORD && _proxyAuthMethod == SocksAuthentication.None)
        {
            _tcpClient.Close();
            string msg = "The proxy destination requires a username and password for authentication. If you received this error attempting to connect to the Tor network provide an string empty value for ProxyUserName and ProxyPassword.";
            Debug.WriteLine(msg);
            return;
            //throw new ProxyException(msg);
        }

        if (acceptedAuthMethod == SOCKS5_AUTH_METHOD_USERNAME_PASSWORD)
        {

            // USERNAME / PASSWORD SERVER REQUEST
            // Once the SOCKS V5 server has started, and the client has selected the
            // Username/Password Authentication protocol, the Username/Password
            // subnegotiation begins.  This begins with the client producing a
            // Username/Password request:
            //
            //       +----+------+----------+------+----------+
            //       |VER | ULEN |  UNAME   | PLEN |  PASSWD  |
            //       +----+------+----------+------+----------+
            //       | 1  |  1   | 1 to 255 |  1   | 1 to 255 |
            //       +----+------+----------+------+----------+

            // create a data structure (binary array) containing credentials
            // to send to the proxy server which consists of clear username and password data
            byte[] credentials = new byte[_proxyUsername.Length + _proxyPassword.Length + 3];

            // for SOCKS5 username/password authentication the VER field must be set to 0x01
            //  http://en.wikipedia.org/wiki/SOCKS
            //      field 1: version number, 1 byte (must be 0x01)"
            credentials[0] = 0x01;
            credentials[1] = (byte)_proxyUsername.Length;
            Array.Copy(Encoding.ASCII.GetBytes(_proxyUsername), 0, credentials, 2, _proxyUsername.Length);
            credentials[_proxyUsername.Length + 2] = (byte)_proxyPassword.Length;
            Array.Copy(Encoding.ASCII.GetBytes(_proxyPassword), 0, credentials, _proxyUsername.Length + 3, _proxyPassword.Length);

            // USERNAME / PASSWORD SERVER RESPONSE
            // The server verifies the supplied UNAME and PASSWD, and sends the
            // following response:
            //
            //   +----+--------+
            //   |VER | STATUS |
            //   +----+--------+
            //   | 1  |   1    |
            //   +----+--------+
            //
            // A STATUS field of X'00' indicates success. If the server returns a
            // `failure' (STATUS value other than X'00') status, it MUST close the
            // connection.

            // transmit credentials to the proxy server
            await stream.WriteAsync(credentials);

            // read the response from the proxy server
            byte[] crResponse = new byte[2];
            await stream.ReadAsync(crResponse);

            // check to see if the proxy server accepted the credentials
            if (crResponse[1] != 0)
            {
                _tcpClient.Close();
                string msg = "Proxy authentification failure! The proxy server has reported that the userid and/or password is not valid.";
                Debug.WriteLine(msg);
                return;
                //throw new ProxyException(msg);
            }
        }
    }

    private byte GetDestAddressType(string host)
    {
        bool result = IPAddress.TryParse(host, out IPAddress? ipAddr);

        if (!result || ipAddr == null)
            return SOCKS5_ADDRTYPE_DOMAIN_NAME;

        return ipAddr.AddressFamily switch
        {
            AddressFamily.InterNetwork => SOCKS5_ADDRTYPE_IPV4,
            AddressFamily.InterNetworkV6 => SOCKS5_ADDRTYPE_IPV6,
            _ => throw new Exception($"The host addess {host} of type \'{Enum.GetName(typeof(AddressFamily), ipAddr.AddressFamily)}\' is not a supported address type. The supported types are InterNetwork and InterNetworkV6."),
        };
    }

    private byte[]? GetDestAddressBytes(byte addressType, string host)
    {
        switch (addressType)
        {
            case SOCKS5_ADDRTYPE_IPV4:
            case SOCKS5_ADDRTYPE_IPV6:
                return IPAddress.Parse(host).GetAddressBytes();
            case SOCKS5_ADDRTYPE_DOMAIN_NAME:
                //  create a byte array to hold the host name bytes plus one byte to store the length
                byte[] bytes = new byte[host.Length + 1];
                //  if the address field contains a fully-qualified domain name.  The first
                //  octet of the address field contains the number of octets of name that
                //  follow, there is no terminating NUL octet.
                bytes[0] = Convert.ToByte(host.Length);
                Encoding.ASCII.GetBytes(host).CopyTo(bytes, 1);
                return bytes;
            default:
                return null;
        }
    }

    private byte[] GetDestPortBytes(int value)
    {
        byte[] array = new byte[2];
        array[0] = Convert.ToByte(value / 256);
        array[1] = Convert.ToByte(value % 256);
        return array;
    }

    private async Task SendCommand(byte command, string destinationHost, int destinationPort)
    {
        if (_tcpClient == null) return;

        NetworkStream stream = _tcpClient.GetStream();

        byte addressType = GetDestAddressType(destinationHost);
        byte[]? destAddr = GetDestAddressBytes(addressType, destinationHost);
        if (destAddr == null) return;

        byte[] destPort = GetDestPortBytes(destinationPort);

        //  The connection request is made up of 6 bytes plus the
        //  length of the variable address byte array
        //
        //  +----+-----+-------+------+----------+----------+
        //  |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
        //  +----+-----+-------+------+----------+----------+
        //  | 1  |  1  | X'00' |  1   | Variable |    2     |
        //  +----+-----+-------+------+----------+----------+
        //
        // * VER protocol version: X'05'
        // * CMD
        //   * CONNECT X'01'
        //   * BIND X'02'
        //   * UDP ASSOCIATE X'03'
        // * RSV RESERVED
        // * ATYP address itemType of following address
        //   * IP V4 address: X'01'
        //   * DOMAINNAME: X'03'
        //   * IP V6 address: X'04'
        // * DST.ADDR desired destination address
        // * DST.PORT desired destination port in network octet order            

        byte[] request = new byte[4 + destAddr.Length + 2];
        request[0] = SOCKS5_VERSION_NUMBER;
        request[1] = command;
        request[2] = SOCKS5_RESERVED;
        request[3] = addressType;
        destAddr.CopyTo(request, 4);
        destPort.CopyTo(request, 4 + destAddr.Length);

        // send connect request.
        await stream.WriteAsync(request);

        //  PROXY SERVER RESPONSE
        //  +----+-----+-------+------+----------+----------+
        //  |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
        //  +----+-----+-------+------+----------+----------+
        //  | 1  |  1  | X'00' |  1   | Variable |    2     |
        //  +----+-----+-------+------+----------+----------+
        //
        // * VER protocol version: X'05'
        // * REP Reply field:
        //   * X'00' succeeded
        //   * X'01' general SOCKS server failure
        //   * X'02' connection not allowed by ruleset
        //   * X'03' Network unreachable
        //   * X'04' Host unreachable
        //   * X'05' Connection refused
        //   * X'06' TTL expired
        //   * X'07' Command not supported
        //   * X'08' Address itemType not supported
        //   * X'09' to X'FF' unassigned
        // RSV RESERVED
        // ATYP address itemType of following address

        byte[] response = new byte[255];

        // read proxy server response
        await stream.ReadAsync(response);

        byte replyCode = response[1];

        //  evaluate the reply code for an error condition
        if (replyCode != SOCKS5_CMD_REPLY_SUCCEEDED)
            HandleProxyCommandError(response, destinationHost, destinationPort);
    }

    private void HandleProxyCommandError(byte[] response, string destinationHost, int destinationPort)
    {
        byte replyCode = response[1];
        byte addrType = response[3];
        string addr = "";
        short port = 0;

        switch (addrType)
        {
            case SOCKS5_ADDRTYPE_DOMAIN_NAME:
                int addrLen = Convert.ToInt32(response[4]);
                byte[] addrBytes = new byte[addrLen];
                for (int i = 0; i < addrLen; i++)
                    addrBytes[i] = response[i + 5];
                addr = Encoding.ASCII.GetString(addrBytes);
                byte[] portBytesDomain = new byte[2];
                portBytesDomain[0] = response[6 + addrLen];
                portBytesDomain[1] = response[5 + addrLen];
                port = BitConverter.ToInt16(portBytesDomain, 0);
                break;

            case SOCKS5_ADDRTYPE_IPV4:
                byte[] ipv4Bytes = new byte[4];
                for (int i = 0; i < 4; i++)
                    ipv4Bytes[i] = response[i + 4];
                IPAddress ipv4 = new(ipv4Bytes);
                addr = ipv4.ToString();
                byte[] portBytesIpv4 = new byte[2];
                portBytesIpv4[0] = response[9];
                portBytesIpv4[1] = response[8];
                port = BitConverter.ToInt16(portBytesIpv4, 0);
                break;

            case SOCKS5_ADDRTYPE_IPV6:
                byte[] ipv6Bytes = new byte[16];
                for (int i = 0; i < 16; i++)
                    ipv6Bytes[i] = response[i + 4];
                IPAddress ipv6 = new(ipv6Bytes);
                addr = ipv6.ToString();
                byte[] portBytesIpv6 = new byte[2];
                portBytesIpv6[0] = response[21];
                portBytesIpv6[1] = response[20];
                port = BitConverter.ToInt16(portBytesIpv6, 0);
                break;
        }

        string proxyErrorText = replyCode switch
        {
            SOCKS5_CMD_REPLY_GENERAL_SOCKS_SERVER_FAILURE => "a general socks destination failure occurred",
            SOCKS5_CMD_REPLY_CONNECTION_NOT_ALLOWED_BY_RULESET => "the connection is not allowed by proxy destination rule set",
            SOCKS5_CMD_REPLY_NETWORK_UNREACHABLE => "the network was unreachable",
            SOCKS5_CMD_REPLY_HOST_UNREACHABLE => "the host was unreachable",
            SOCKS5_CMD_REPLY_CONNECTION_REFUSED => "the connection was refused by the remote network",
            SOCKS5_CMD_REPLY_TTL_EXPIRED => "the time to live (TTL) has expired",
            SOCKS5_CMD_REPLY_COMMAND_NOT_SUPPORTED => "the command issued by the proxy client is not supported by the proxy destination",
            SOCKS5_CMD_REPLY_ADDRESS_TYPE_NOT_SUPPORTED => "the address type specified is not supported",
            _ => $"an unknown SOCKS reply with the code value '{replyCode}' was received",
        };
        string responseText = response != null ? Utils.HexEncode(response) : "";
        string exceptionMsg = $"Proxy Error: {proxyErrorText} for destination host {destinationHost} port number {destinationPort}. Server response (hex): {responseText}.";

        Debug.WriteLine(exceptionMsg);
        return;
        // throw new ProxyException(exceptionMsg);
    }

    // Async Methods
    private BackgroundWorker? _asyncWorker;
    private Exception? _asyncException;
    bool _asyncCancelled;

    /// <summary>
    /// Gets a value indicating whether an asynchronous operation is running.
    /// </summary>
    /// <remarks>Returns true if an asynchronous operation is running; otherwise, false.
    /// </remarks>
    public bool IsBusy
    {
        get { return _asyncWorker != null && _asyncWorker.IsBusy; }
    }

    /// <summary>
    /// Gets a value indicating whether an asynchronous operation is cancelled.
    /// </summary>
    /// <remarks>Returns true if an asynchronous operation is cancelled; otherwise, false.
    /// </remarks>
    public bool IsAsyncCancelled
    {
        get { return _asyncCancelled; }
    }

    /// <summary>
    /// Cancels any asychronous operation that is currently active.
    /// </summary>
    public void CancelAsync()
    {
        if (_asyncWorker != null && !_asyncWorker.CancellationPending && _asyncWorker.IsBusy)
        {
            _asyncCancelled = true;
            _asyncWorker.CancelAsync();
        }
    }

    private void CreateAsyncWorker()
    {
        _asyncWorker?.Dispose();
        _asyncException = null;
        _asyncWorker = null;
        _asyncCancelled = false;
        _asyncWorker = new();
    }

    /// <summary>
    /// Event handler for CreateConnectionAsync method completed.
    /// </summary>
    public event EventHandler<CreateConnectionAsyncCompletedEventArgs>? CreateConnectionAsyncCompleted;

    /// <summary>
    /// Asynchronously creates a remote TCP connection through a proxy server to the destination host on the destination port.
    /// </summary>
    /// <param name="destinationHost">Destination host name or IP address.</param>
    /// <param name="destinationPort">Port number to connect to on the destination host.</param>
    /// <returns>
    /// Returns TcpClient object that can be used normally to communicate
    /// with the destination server.
    /// </returns>
    /// <remarks>
    /// This method instructs the proxy server
    /// to make a pass through connection to the specified destination host on the specified
    /// port.  
    /// </remarks>
    public void CreateConnectionAsync(string destinationHost, int destinationPort)
    {
        if (_asyncWorker != null)
        {
            if (_asyncWorker.IsBusy)
                throw new InvalidOperationException("The Socks4 object is already busy executing another asynchronous operation. You can only execute one asychronous method at a time.");

            CreateAsyncWorker();
            _asyncWorker.WorkerSupportsCancellation = true;
            _asyncWorker.DoWork += new DoWorkEventHandler(CreateConnectionAsync_DoWork);
            _asyncWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CreateConnectionAsync_RunWorkerCompleted);
            object[] args = new object[2];
            args[0] = destinationHost;
            args[1] = destinationPort;
            _asyncWorker.RunWorkerAsync(args);
        }
    }

    private void CreateConnectionAsync_DoWork(object? sender, DoWorkEventArgs e)
    {
        try
        {
            if (e.Argument != null)
            {
                object[] args = (object[])e.Argument;
                e.Result = CreateConnection((string)args[0], (int)args[1]);
            }
        }
        catch (Exception ex)
        {
            _asyncException = ex;
        }
    }

    private void CreateConnectionAsync_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        if (e.Result != null)
            CreateConnectionAsyncCompleted?.Invoke(this, new CreateConnectionAsyncCompletedEventArgs(_asyncException, _asyncCancelled, (TcpClient)e.Result));
    }

}