﻿using CrazyChatShareLib;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace CrazyChatClient
{
    public partial class ChatForm : Form
    {
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppender;
        private Socket mainSock;

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
        private void OnFormLoaded(object sender, EventArgs e) {
            IPAddress defaultHostAddress = Utillity.GetIPAddress();
            txtAddress.Text = defaultHostAddress.ToString();
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
        /// 연결 버튼 클릭 이벤트
        /// 서버에 연결 한다
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConnectToServer(object sender, EventArgs e) {
            if (mainSock.Connected) {
                MsgBoxHelper.Error("이미 연결되어 있습니다!");
                return;
            }

            // port 체크
            int port;
            if (!int.TryParse(txtPort.Text, out port)) {
                MsgBoxHelper.Error("포트 번호가 잘못 입력되었거나 입력되지 않았습니다.");
                txtPort.Focus();
                txtPort.SelectAll();
                return;
            }

            // 연결
            try {
                mainSock.Connect(txtAddress.Text, port);
            }
            catch (Exception ex) {
                MsgBoxHelper.Error("연결에 실패했습니다!\n오류 내용: {0}", MessageBoxButtons.OK, ex.Message);
                return;
            }

            // 연결 완료되었다는 메세지를 띄워준다.
            AppendText(txtHistory, "서버와 연결되었습니다.");

            // 연결 완료, 서버에서 데이터가 올 수 있으므로 비동기로 수신 대기한다.
            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = mainSock;
            mainSock.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0, DataReceived, obj);
        }

        /// <summary>
        /// 서버로 부터 데이터 수신하고 화면에 출력
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

            // 서버 ip 주소
            IPEndPoint ip = (IPEndPoint)mainSock.LocalEndPoint;
            string addr = ip.Address.ToString();

            // 문자열을 utf8 형식의 바이트로 변환한다
            byte[] bDts = Encoding.UTF8.GetBytes(addr + '\x01' + tts);

            // 서버에 전송한다.
            mainSock.Send(bDts);

            // 전송 완료 후 텍스트박스에 추가하고, 원래의 내용은 지운다
            AppendText(txtHistory, string.Format("[보냄]{0}: {1}", addr, tts));
            txtTTS.Clear();
        }
    }
}
