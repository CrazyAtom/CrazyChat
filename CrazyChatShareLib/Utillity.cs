using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        static public void DigitFilter(object sender, KeyPressEventArgs e) {
            e.Handled = !(char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar));
        }
    }
}
