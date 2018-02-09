using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrazyChatClient
{
    public partial class ChatForm : Form
    {
        public ChatForm()
        {
            InitializeComponent();
        }

        /* 
         *  입력된 무자가 제어 문자 혹은 10진수 문자일 경우엔 입력 허용
         *  그 외의 문자가 입력된 경우 입력 차단
         */
        private void DigitFilter(object sender, KeyPressEventArgs e)
        {
            e.Handled = !(char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar));
        }
    }
}
