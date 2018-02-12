using CrazyChatShareLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace CrazyChatServer
{
    public partial class ChatForm : Form
    {
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppender;
        private Socket mainSock;
        private IPAddress thisAddress;

        public ChatForm()
        {
            InitializeComponent();
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _textAppender = new AppendTextDelegate(AppendText);
        }

        void AppendText(Control ctrl, string s) {
            if (_textAppender == null) { _textAppender = new AppendTextDelegate(AppendText); }

            // 컨트롤이 생성된 UI thread가 아니면 대리자를 통해 UI thread에서 호출 
            if (ctrl.InvokeRequired) ctrl.Invoke(_textAppender, ctrl, s);
            else {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }

        /// <summary>
        /// 폼 로드 이벤트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OFormLoaded(object sender, EventArgs e) {
            IPHostEntry he = Dns.GetHostEntry(Dns.GetHostName());

            // 처음으로 발견되는 ipv4 주소를 사용
            foreach (IPAddress addr in he.AddressList) {
                if (addr.AddressFamily == AddressFamily.InterNetwork) {
                    thisAddress = addr;
                    break;
                }
            }

            // 주소가 없다면
            if (thisAddress == null) {
                // 로컬호스트 주소를 사용한다
                thisAddress = IPAddress.Loopback;
            }

            txtAddress.Text = thisAddress.ToString();
        }

        /// <summary>
        /// 입력된 문자가 제어 문자 혹은 10진수 문자일 경우엔 입력 허용
        /// 그 외의 문자가 입력된 경우 입력 차단
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">event</param>
        private void DigitFilter(object sender, KeyPressEventArgs e) {
            Utillity.DigitFilter(sender, e);
        }

        /// <summary>
        /// 시작 버튼 클릭 이벤트
        /// 소켓 서버를 시작 시킨다
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">event</param>
        private void BeginStartServer(object sender, EventArgs e) {
            int port;
            if (!int.TryParse(txtPort.Text, out port)) {
                MsgBoxHelper.Error("포트 번호가 잘못 입력되었거나 입력되지 않았습니다.");
                txtPort.Focus();
                txtPort.SelectAll();
                return;
            }

            // 서버에서 클라이언트의 연결 요청을 대기 하기 위해 소켓을 열어 둔다
            IPEndPoint serverEP = new IPEndPoint(thisAddress, port);
            mainSock.Bind(serverEP);
            mainSock.Listen(10);
            AppendText(txtHistory, string.Format("{0} 서버가 실행 되었습니다.", thisAddress.ToString()));

            // 비동기적으로 클라이언트의 연결 요청을 받는다
            mainSock.BeginAccept(AcceptCallback, null);
        }

        List<Socket> connectedClients = new List<Socket>();

        /// <summary>
        /// 클라이언트의 연결요청을 수락하고 접속 클라이언트 리스트에 추가
        /// </summary>
        /// <param name="ar"></param>
        void AcceptCallback(IAsyncResult ar) {
            // 클라이언트의 연결 요청을 수락한다
            Socket client = mainSock.EndAccept(ar);

            // 또 다른 클라이언트의 연결을 대기한다
            mainSock.BeginAccept(AcceptCallback, null);

            // 접속된 클라이언트 데이터 오브젝트 생성
            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = client;

            // 연결된 클라이언트 리스트에 추가해 준다
            connectedClients.Add(client);

            // 텍스트박스에 클라이언트 연결 문구 출력
            AppendText(txtHistory, string.Format("클라이언트 (@ {0})가 연결되었습니다.", client.RemoteEndPoint));

            // 클라이언트의 데이터를 수신
            client.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        /// <summary>
        /// 클라이언트로 부터 데이터를 수신하고 수신된 데이터를 모든 클라이언트에게 전송
        /// </summary>
        /// <param name="ar"></param>
        void DataReceived(IAsyncResult ar) {
            // BeginReceive에서 추가적으로 넘어온 데이터를 AsyncObject 형식으로 변환한다
            AsyncObject obj = (AsyncObject)ar.AsyncState;

            // 데이터 수신을 끝낸다
            int received = obj.WorkingSocket.EndReceive(ar);

            // 받은 데이터가 없으면(연결끊어짐) 끝낸다
            if (received <= 0) {
                obj.WorkingSocket.Close();
                return;
            }

            // 클라이언트가 보낸 데이터는 obj.Buffer에 바이트 형식으로 저장된다
            // 따라서 이 데이터는 System.Text.Encoding 클래스의 GetString 함수를 이용하여 문자열로 변환해야 사용이 가능하다
            string text = Encoding.UTF8.GetString(obj.Buffer);

            // 0x01 기준으로 문자열 구분
            // tokens[0] - 보낸 사람 IP
            // tokens[1] - 보낸 메세지
            string[] tokens = text.Split('\x01');
            string ip = tokens[0];
            string msg = tokens[1];

            // 텍스트박스에 추가
            // 비동기식으로 작업하기 때문에 폼의 UI 스레드에서 처리 해야 한다
            // 대리자를 통해 처리
            AppendText(txtHistory, string.Format("[받음]{0}: {1}", ip, msg));

            // 모든 클라이언트에게 데이터를 보낸다
            for (int i = connectedClients.Count - 1; i >= 0; i--) {
                Socket socket = connectedClients[i];
                // 현재 작업 클라이언트는 제외
                if (socket != obj.WorkingSocket) {
                    try { socket.Send(obj.Buffer); }
                    catch {
                        // 오류 발생시 전송 취소 하고 리스트에서만 삭제
                        try { socket.Dispose(); }
                        catch { }
                        connectedClients.RemoveAt(i);
                    }
                }
            }

            // 데이터를 받은 후엔 다시 버퍼를 비워주고 같은 방법으로 수신을 대기한다
            obj.ClearBuffer();

            // 수신대기
            obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        /// <summary>
        /// 보내기 버튼 이벤트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSendData(object sender, EventArgs e) {
            // 서버가 대기중인지 확인
            if (!mainSock.IsBound) {
                MsgBoxHelper.Warn("서버가 실행되고 있지 않습니다!");
                return;
            }

            // 보낼 텍스트
            string tts = txtTTS.Text.Trim();
            if (string.IsNullOrEmpty(tts)) {
                MsgBoxHelper.Warn("텍스트가 입력되지 않았습니다!");
                txtTTS.Focus();
                return;
            }

            // 문자열을 utf8 형식의 바이트로 변환한다.
            byte[] bDts = Encoding.UTF8.GetBytes(thisAddress.ToString() + '\x01' + tts);

            // 연결된 모든 클라이언트에게 전송한다
            for (int i = connectedClients.Count - 1; i >= 0; i--) {
                Socket socket = connectedClients[i];
                try { socket.Send(bDts); }
                catch {
                    // 오류 발생하면 전송 취소하고 리스트에서 삭제
                    try { socket.Dispose(); }
                    catch { }
                    connectedClients.RemoveAt(i);
                }
            }

            // 전송 완료 후 텍스트박스에 추가하고, 원래의 내용은 지운다
            AppendText(txtHistory, string.Format("[보냄]{0}: {1}", thisAddress.ToString(), tts));
            txtTTS.Clear();
        }
    }
}
