using System;
using APIShared.Presentation;
using Noesis;

namespace AutoSendTest
{
    internal sealed partial class AutoSendView
    {
        private AllyGoodsFrameRegistration frameRegistration;

        private void RegisterFrame()
        {
            // These are AutoSend's presentation choices. The shared broker owns
            // placement and reversible visibility, without knowing a Vanilla style.
            var options = new AllyGoodsFrameOptions(new Thickness(16, 12, 16, 24),
                new Thickness(18), 260, 8, 8);
            if (!AllyGoodsPanelExtensions.TryRegisterFramePresentation(AutoSendPlugin.Guid, "auto-send-frame", CreateFrame,
                options, out frameRegistration, out string reason, 100)) throw new InvalidOperationException(reason);
        }

        private static FrameworkElement CreateFrame(AllyGoodsFrameContext context)
        {
            if (!(context?.OriginalFrame is Button original) || original.Command != null || original.Content != null)
                throw new InvalidOperationException("AutoSend requires the decorative Vanilla goods-frame button.");
            if (!(context.Host?.TryFindResource("BDR_SH1DE60") is Style style) || !ReferenceEquals(original.Style, style))
                throw new InvalidOperationException("The goods-frame style is not the expected Vanilla resource.");
            if (!ReferenceEquals(original.ReadLocalValue(Control.TemplateProperty), DependencyProperty.UnsetValue))
                throw new InvalidOperationException("A foreign local goods-frame template is preserved.");
            // Keep the original style's complete template and sprite behavior.
            // The broker makes this fresh decoration noninteractive and sizes it.
            return new Button { Style = style, Opacity = original.Opacity };
        }
    }
}
