namespace PcToolkit
{
    using System.Drawing;
    using System.Windows.Forms;

    internal static class Theme
    {
        public static readonly Color Back = Color.FromArgb(30, 30, 46);
        public static readonly Color Surface = Color.FromArgb(24, 24, 37);
        public static readonly Color Card = Color.FromArgb(49, 50, 68);
        public static readonly Color CardHover = Color.FromArgb(69, 71, 90);
        public static readonly Color Border = Color.FromArgb(88, 91, 112);
        public static readonly Color Accent = Color.FromArgb(137, 180, 250);
        public static readonly Color Ink = Color.FromArgb(17, 17, 27);
        public static readonly Color Text = Color.FromArgb(205, 214, 244);
        public static readonly Color SubText = Color.FromArgb(147, 153, 178);
        public static readonly Color Good = Color.FromArgb(166, 227, 161);
        public static readonly Color Bad = Color.FromArgb(243, 139, 168);

        public static Font MakeFont(float size, bool bold = false)
        {
            return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        }

        public static Label MakeLabel(string text, float size = 9f, bool bold = false, Color? color = null)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = MakeFont(size, bold),
                ForeColor = color ?? Text,
                BackColor = Color.Transparent
            };
        }

        public static Button MakeButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Card,
                ForeColor = Text,
                Font = MakeFont(9f),
                Cursor = Cursors.Hand,
                Padding = new Padding(10, 6, 10, 6)
            };
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = CardHover;
            b.FlatAppearance.MouseDownBackColor = Accent;
            return b;
        }
    }

    internal static class Format
    {
        public static string Bytes(long b)
        {
            return Bytes((decimal)b);
        }

        public static string Bytes(ulong b)
        {
            return Bytes((decimal)b);
        }

        private static string Bytes(decimal b)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            var v = b;
            int i = 0;
            while (v >= 1024m && i < units.Length - 1)
            {
                v /= 1024m;
                i++;
            }
            return string.Format("{0:0.#} {1}", v, units[i]);
        }
    }
}
