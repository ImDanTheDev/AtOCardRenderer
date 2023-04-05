using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AtOCCardRenderer
{
    public class CSVBuilder
    {
        private readonly string _fileName;
        private readonly PropertyInfo[] _cardDataProperties;

        private readonly StringBuilder _builder;

        public CSVBuilder(string fileName)
        {
            _fileName = fileName;
            _cardDataProperties = typeof(CardData).GetProperties();
            _builder = new StringBuilder();
            Reset();
        }

        public void AddCard(CardData card, List<GameObject> elements)
        {
            var properties = _cardDataProperties.Select(x =>
            {
                var value = x.GetValue(card);
                var strValue = value?.ToString()?.Replace("\n", "");
                return $"\"{strValue}\"";
            });

            _builder.AppendJoin(',', properties);
            _builder.Append(',');
            _builder.AppendJoin('|', elements.Select(x => x.name));
            _builder.AppendLine();
        }

        public void Reset()
        {
            _builder.Clear();
            _builder.AppendJoin(',', _cardDataProperties.Select(x => $"\"{x.Name}\""));
            _builder.AppendLine(",\"sections\"");
        }

        public void Write()
        {
            File.WriteAllText(_fileName, _builder.ToString());
        }
    }
}