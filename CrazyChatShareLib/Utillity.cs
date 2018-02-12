namespace CrazyChatShareLib
{
    public class Utillity
    {
        /// <summary>
        /// 입력된 문자가 제어 문자 혹은 10진수 문자일 경우엔 입력 허용
        /// 그 외의 문자가 입력된 경우 입력 차단
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">event</param>
        static public void DigitFilter(object sender, System.Windows.Forms.KeyPressEventArgs e) {
            e.Handled = !(char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar));
        }

        /// <summary>
        /// 처음으로 발견되는 ip 검색
        /// 검색된 ip가 없다면 로컬 호스트를 주소로 사용
        /// </summary>
        /// <returns>ip address</returns>
        static public System.Net.IPAddress GetIPAddress() {
            System.Net.IPHostEntry he = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());

            // 처음으로 발견되는 ipv4 주소를 사용
            System.Net.IPAddress defaultHostAddress = null;
            foreach (System.Net.IPAddress addr in he.AddressList) {
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                    defaultHostAddress = addr;
                    break;
                }
            }

            // 검색된 ip가 없다면 로컬호스트를 주소로 사용
            return (defaultHostAddress == null) ? System.Net.IPAddress.Loopback : defaultHostAddress;
        }
    }
}
