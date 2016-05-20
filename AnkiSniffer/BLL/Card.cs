using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnkiSniffer.BLL {
    public class Card {
		public string Tags { get; set; }

		public string Content { get; set; }

		public string Word { get; set; }

		public List<string> Translate { get; set; }

		public string FlatTranslate
		{
			get
			{
				if (Translate == null)
					return string.Empty;
				return string.Join (" | ", Translate);
			}
		}

		public List<string> Examples { get; set; }

		public string FlatExamples
		{
			get
			{
				if (Examples == null)
					return string.Empty;
				return string.Join (" | ", Examples);
			}
		}
	}
}
