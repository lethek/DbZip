using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

using Fclp;
using Fclp.Internals;
using Fclp.Internals.Extensions;


namespace DbZip
{

	public class TidyCommandLineOptionFormatter : ICommandLineOptionFormatter
	{

		private const int BuilderCapacity = 128;
		private const int DefaultMaximumLength = 80; // default console width
		private int? _maximumDisplayWidth;

		/// <summary>
		/// The text format used in this formatter.
		/// </summary>
		public const string TextFormat = "\t{0}\t\t{1}\n";

		/// <summary>
		/// If true, outputs a header line above the option list. If false, the header is omitted. Default is true.
		/// </summary>
		private bool ShowHeader { get { return Header != null; } }


		/// <summary>
		/// Gets or sets the header to display before the printed options.
		/// </summary>
		public string Header { get; set; }


		public string Usage { get; set; }


		/// <summary>
		/// Gets or sets the text to use as <c>Value</c> header. This should be localised for the end user.
		/// </summary>
		public string ValueText { get; set; }


		/// <summary>
		/// Gets or sets the text to use as the <c>Description</c> header. This should be localised for the end user.
		/// </summary>
		public string DescriptionText { get; set; }


		/// <summary>
		/// Gets or sets the text to use when there are no options. This should be localised for the end user.
		/// </summary>
		public string NoOptionsText { get; set; }


		public int MaximumDisplayWidth
		{
			get { return _maximumDisplayWidth ?? DefaultMaximumLength; }
			set { _maximumDisplayWidth = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether the format of options should contain dashes.
		/// It modifies behavior of <see cref="AddOptions{T}(T)"/> method.
		/// </summary>
		public bool AddDashesToOption { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether to add an additional line after the description of the option.
		/// </summary>
		public bool AdditionalNewLineAfterOption { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether to add the values of an enum after the description of the option.
		/// </summary>
		public bool AddEnumValuesToHelpText { get; set; }


		/// <summary>
		/// Initialises a new instance of the <see cref="T:Fclp.Internals.CommandLineOptionFormatter"/> class.
		/// </summary>
		public TidyCommandLineOptionFormatter()
		{
			ValueText = "Value";
			DescriptionText = "Description";
			NoOptionsText = "No options have been setup";
			AdditionalNewLineAfterOption = true;
		}


		/// <summary>
		/// Formats the list of <see cref="T:Fclp.Internals.ICommandLineOption"/> to be displayed to the user.
		/// </summary>
		/// <param name="options">The list of <see cref="T:Fclp.Internals.ICommandLineOption"/> to format.</param>
		/// <returns>
		/// A <see cref="T:System.String"/> representing the format
		/// </returns>
		/// <exception cref="T:System.ArgumentNullException">If <paramref name="options"/> is <c>null</c>.</exception>
		public string Format(IEnumerable<ICommandLineOption> options)
		{
			if (options == null) {
				throw new ArgumentNullException("options");
			}

			var list = options.ToList();
			if (!list.Any()) {
				return NoOptionsText;
			}

			var title = GetAttribute<AssemblyTitleAttribute>();
			var version = GetAttribute<AssemblyInformationalVersionAttribute>();
			var copyright = GetAttribute<AssemblyCopyrightAttribute>();

			var builder = new StringBuilder(BuilderCapacity);
			builder.AppendLine();
			if (ShowHeader) {
				builder.AppendLine(Header);
				builder.AppendLine();
			}

			builder.AppendLine(title.Title + " " + version.InformationalVersion);
			builder.AppendLine(copyright.Copyright);
			if (!String.IsNullOrEmpty(Usage)) {
				builder.AppendLine(Usage);
			}

			builder.AppendLine();
			AddOptionsImpl(builder, list, MaximumDisplayWidth);

			return builder.ToString();
		}


		/// <summary>
		/// Formats the short and long names into one <see cref="T:System.String"/>.
		/// 
		/// </summary>
		private static string FormatValue(ICommandLineOption cmdOption)
		{
			if (cmdOption.ShortName.IsNullOrWhiteSpace()) {
				return cmdOption.LongName;
			}
			if (cmdOption.LongName.IsNullOrWhiteSpace()) {
				return cmdOption.ShortName;
			}
			return "-" + cmdOption.ShortName + ", --" + cmdOption.LongName;
		}


		private static TAttribute GetAttribute<TAttribute>()
			where TAttribute : Attribute
		{
			var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
			var attributes = assembly.GetCustomAttributes(typeof(TAttribute), false);
			return (TAttribute)attributes.FirstOrDefault();
		}


		private static int GetMaxLength(IEnumerable<ICommandLineOption> optionList)
		{
			var length = 0;
			foreach (var option in optionList) {
				var optionLength = 0;
				if (option.HasShortName) {
					optionLength += 2;
				}
				if (option.HasLongName) {
					optionLength += option.LongName.Length + 2;
				}
				if (option.HasShortName && option.HasLongName) {
					optionLength += 2; // ", "
				}
				length = Math.Max(length, optionLength);
			}
			return length;
		}


		private void AddOptionsImpl(StringBuilder builder, IEnumerable<ICommandLineOption> optionList, int maximumLength)
		{
			var maxLength = GetMaxLength(optionList);
			var remainingSpace = maximumLength - (maxLength + 6);
			foreach (var option in optionList) {
				AddOption(builder, maxLength, option, remainingSpace);
			}
		}


		private void AddOption(StringBuilder builder, int maxLength, ICommandLineOption option, int widthOfHelpText)
		{
			builder.Append("  ");
			var optionName = new StringBuilder(maxLength);
			if (option.HasShortName) {
				optionName.Append('-');
				optionName.AppendFormat("{0}", option.ShortName);
				if (option.HasLongName) {
					optionName.Append(", ");
				}
			}

			if (option.LongName.Length > 0) {
				optionName.Append("--");
				optionName.AppendFormat("{0}", option.LongName);
			}

			builder.Append(
				optionName.Length < maxLength
					? optionName.ToString().PadRight(maxLength)
					: optionName.ToString()
			);

			builder.Append("    ");
			var optionHelpText = option.Description;

			if (option.IsRequired) {
				optionHelpText = "Required. " + optionHelpText;
			}

			if (!String.IsNullOrEmpty(optionHelpText)) {
				do {
					var wordBuffer = 0;
					var words = optionHelpText.Split(' ');
					for (var i = 0; i < words.Length; i++) {
						if (words[i].Length < (widthOfHelpText - wordBuffer)) {
							builder.Append(words[i]);
							wordBuffer += words[i].Length;
							if ((widthOfHelpText - wordBuffer) > 1 && i != words.Length - 1) {
								builder.Append(" ");
								wordBuffer++;
							}
						} else if (words[i].Length >= widthOfHelpText && wordBuffer == 0) {
							builder.Append(words[i].Substring(0, widthOfHelpText));
							wordBuffer = widthOfHelpText;
							break;
						} else {
							break;
						}
					}

					optionHelpText = optionHelpText.Substring(Math.Min(wordBuffer, optionHelpText.Length)).Trim();
					if (optionHelpText.Length > 0) {
						builder.Append(Environment.NewLine);
						builder.Append(new String(' ', maxLength + 6));
					}
				}
				while (optionHelpText.Length > widthOfHelpText);
			}

			builder.Append(optionHelpText);
			builder.Append(Environment.NewLine);
			if (AdditionalNewLineAfterOption) {
				builder.Append(Environment.NewLine);
			}
		}


		private static void AddLine(StringBuilder builder, string value, int maximumLength)
		{
			if (builder.Length > 0) {
				builder.Append(Environment.NewLine);
			}

			do {
				var wordBuffer = 0;
				var words = value.Split(' ');
				for (var i = 0; i < words.Length; i++) {
					if (words[i].Length < (maximumLength - wordBuffer)) {
						builder.Append(words[i]);
						wordBuffer += words[i].Length;
						if ((maximumLength - wordBuffer) > 1 && i != words.Length - 1) {
							builder.Append(" ");
							wordBuffer++;
						}
					} else if (words[i].Length >= maximumLength && wordBuffer == 0) {
						builder.Append(words[i].Substring(0, maximumLength));
						wordBuffer = maximumLength;
						break;
					} else {
						break;
					}
				}

				value = value.Substring(Math.Min(wordBuffer, value.Length));
				if (value.Length > 0) {
					builder.Append(Environment.NewLine);
				}
			} while (value.Length > maximumLength);

			builder.Append(value);
		}

	}

}
