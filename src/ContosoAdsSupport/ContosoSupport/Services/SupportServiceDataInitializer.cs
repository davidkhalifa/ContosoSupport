using ContosoSupport.Models;

namespace ContosoSupport.Services
{
    internal sealed class SupportServiceDataInitializer(ISupportService supportService) : IHostedService
    {
        private readonly ISupportService supportService = supportService;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (0 == await supportService.GetDocumentCountAsync().ConfigureAwait(false))
            {
                // Create a new SupportCase if collection is empty
                await Task.WhenAll(
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 1", Owner = "Shehab Fawzy", IsComplete = true, Description = "Proactively expedite parallel technology rather than wireless models. Competently syndicate 2.0 users via B2B technology. Conveniently syndicate end-to-end strategic theme areas before proactive e-business. Quickly reinvent long-term high-impact growth strategies before plug-and-play web-readiness. Synergistically extend robust growth strategies before orthogonal intellectual capital." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 2", Owner = "Devidas Gupta", IsComplete = false, Description = "Synergistically create next-generation innovation and effective innovation. Seamlessly simplify bricks-and-clicks information without high-payoff strategic theme areas. Distinctively deliver accurate vortals vis-a-vis cooperative users." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 3", Owner = "Nick Hauenstein", IsComplete = false, Description = "Collaboratively productivate performance based synergy with adaptive bandwidth. Intrinsicly communicate unique outsourcing through enterprise-wide interfaces. Interactively deliver next-generation initiatives vis-a-vis fully researched results. Assertively negotiate optimal \"outside the box\" thinking via fully researched ideas. Appropriately incentivize enterprise metrics after high-payoff convergence." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 4", Owner = "Tim Colbert", IsComplete = false, Description = "Competently network premier products whereas innovative e-tailers. Interactively leverage other's interactive products before visionary solutions. Seamlessly transition orthogonal niche markets whereas standards compliant manufactured products. Rapidiously engineer extensive solutions whereas cooperative quality vectors. Efficiently reintermediate prospective data whereas low-risk high-yield ideas." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 5", Owner = "Anne Hamilton", IsComplete = false, Description = "Continually pursue diverse content before virtual convergence. Completely supply interoperable growth strategies without progressive scenarios. Dramatically pursue cross-platform portals after functionalized niches. Holisticly orchestrate effective niches rather than turnkey paradigms. Credibly transition business." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 6", Owner = "Shehab Fawzy", IsComplete = true, Description = "Credibly synthesize accurate alignments with just in time convergence. Appropriately reconceptualize sticky resources vis-a-vis goal-oriented leadership skills. Efficiently matrix market-driven platforms rather than frictionless." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 7", Owner = "Nick Hauenstein", IsComplete = true, Description = "Seamlessly plagiarize ubiquitous expertise whereas market positioning deliverables. Efficiently unleash functional technology vis-a-vis distributed paradigms. Interactively recaptiualize state of the art mindshare after interactive niche markets. Credibly simplify virtual strategic theme areas via efficient users. Conveniently pontificate cross-platform catalysts for change through enabled ideas." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 8", Owner = "Devidas Gupta", IsComplete = false, Description = "Assertively synergize interdependent leadership skills and one-to-one outsourcing. Seamlessly engage timely functionalities after multifunctional outsourcing. Distinctively empower flexible." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 9", Owner = "Tim Colbert", IsComplete = true, Description = "Quickly myocardinate end-to-end e-tailers whereas error-free testing procedures. Assertively exploit mission-critical internal or \"organic\" sources whereas vertical resources. Synergistically facilitate client-centered web-readiness without effective supply chains." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 10", Owner = "Tim Colbert", IsComplete = false, Description = "Energistically repurpose business intellectual capital before unique sources. Competently deploy user-centric opportunities for leading-edge \"outside the box\" thinking." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 11", Owner = "Anne Hamilton", IsComplete = false, Description = "Efficiently enhance sustainable manufactured products vis-a-vis effective schemas. Progressively procrastinate one-to-one results after reliable internal or \"organic\" sources. Appropriately fabricate customized e-tailers without quality expertise. Professionally unleash visionary synergy via backend customer service. Uniquely develop enterprise-wide content rather than backward-compatible technologies." }),
                    supportService.CreateAsync(new SupportCase { Title = "Support Case 12", Owner = "Shehab Fawzy", IsComplete = true, Description = "Monotonectally revolutionize B2B expertise rather than front-end mindshare. Efficiently synergize stand-alone meta-services vis-a-vis interdependent functionalities. Synergistically benchmark cost effective functionalities rather than worldwide partnerships. Efficiently scale premier products whereas." })).ConfigureAwait(false);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
