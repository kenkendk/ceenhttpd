using Ganss.XSS;
using Markdig;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Wrapper class for sharing configuration of markdown render and sanitizer
    /// </summary>
    public class MarkdownRenderer : IModule
    {
        /// <summary>
        /// The markdown pipeline
        /// </summary>
        private readonly MarkdownPipeline m_mdpipeline;

        /// <summary>
        /// The HTML sanitizer
        /// </summary>
        private readonly HtmlSanitizer m_sanitizer;

        /// <summary>
        /// Configure the renderer and sanitizer
        /// </summary>
        public MarkdownRenderer()
        {
            LoaderContext.RegisterSingletonInstance(this);
            m_mdpipeline = new MarkdownPipelineBuilder().Build();
            m_sanitizer = new HtmlSanitizer();
        }

        /// <summary>
        /// Renders the input markdown as html and sanitizes the output
        /// </summary>
        /// <param name="md">The markdown input</param>
        /// <returns>The html output</returns>
        public static string RenderAsHtml(string md)
        {
            var inst = LoaderContext.SingletonInstance<MarkdownRenderer>();
            return inst.m_sanitizer.Sanitize(Markdown.ToHtml(md, inst.m_mdpipeline));
        }

        /// <summary>
        /// Renders the input markdown as plain text
        /// </summary>
        /// <param name="md">The markdown input</param>
        /// <returns>The plain-text output</returns>
        public static string RenderAsText(string md)
        {
            var inst = LoaderContext.SingletonInstance<MarkdownRenderer>();
            return Markdown.ToPlainText(md, inst.m_mdpipeline);
        }

    }
}