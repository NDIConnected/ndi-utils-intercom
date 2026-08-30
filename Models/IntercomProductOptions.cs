namespace NDIIntercom.Models
{
    /// <summary>
    /// Product identity (16ch full vs 2ch light). Set <see cref="IntercomRuntime.Product"/> at process entry before config/engine use.
    /// </summary>
    public sealed class IntercomProductOptions
    {
        public required int MaxChannels { get; init; }
        public required string DataFolderName { get; init; }
        public required string ProductDisplayName { get; init; }
        public required string UiTitleShort { get; init; }
        public int DefaultWebPort { get; init; }

        /// <summary>NDI <c>ndi_product</c> long_name attribute (visible in NDI tools).</summary>
        public required string NdiProductLongName { get; init; }

        /// <summary>NDI <c>ndi_product</c> short_name attribute.</summary>
        public required string NdiProductShortName { get; init; }

        /// <summary>NDI <c>ndi_product</c> model_name (e.g. Intercom-16CH / Intercom-2CH).</summary>
        public required string NdiModelName { get; init; }

        /// <summary>NDI <c>ndi_product</c> version string.</summary>
        public string NdiProductVersion { get; init; } = "1.7.9";
    }

    public static class IntercomProducts
    {
        public static IntercomProductOptions Full { get; } = new IntercomProductOptions
        {
            MaxChannels = 16,
            DataFolderName = "NDI Intercom16",
            ProductDisplayName = "NDI Intercom16",
            UiTitleShort = "Intercom16",
            DefaultWebPort = 5016,
            NdiProductLongName = "NDI Intercom16 - Professional Audio Intercom System",
            NdiProductShortName = "NDI Intercom16",
            NdiModelName = "Intercom-16CH",
            NdiProductVersion = "1.7.9"
        };

        public static IntercomProductOptions Light { get; } = new IntercomProductOptions
        {
            MaxChannels = 2,
            DataFolderName = "NDI Intercom2",
            ProductDisplayName = "NDI Intercom2",
            UiTitleShort = "Intercom2",
            DefaultWebPort = 5017,
            NdiProductLongName = "NDI Intercom2 - Professional Audio Intercom System",
            NdiProductShortName = "NDI Intercom2",
            NdiModelName = "Intercom-2CH",
            NdiProductVersion = "1.7.9"
        };
    }

    public static class IntercomRuntime
    {
        private static IntercomProductOptions? _product;

        public static IntercomProductOptions Product
        {
            get => _product ?? IntercomProducts.Full;
            set => _product = value;
        }
    }
}
