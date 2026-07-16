namespace OriginLab.DocumentGeneration;

public abstract class ProblemSummarizer
{
    public abstract void WriteSummary(ProblemRecorder recorder, TextWriter textWriter);

    public sealed class Local : ProblemSummarizer
    {
        public override void WriteSummary(ProblemRecorder recorder, TextWriter textWriter)
        {
            if (recorder.Any)
            {
                var problems = recorder.GetRecords();

                textWriter.WriteLine();
                textWriter.WriteLine("Problems:");

                problems.Sort((x, y) => y.locations.Count - x.locations.Count);

                foreach (var (category, locations) in problems)
                {
                    textWriter.WriteLine();
                    textWriter.WriteLine("==============================================");
                    textWriter.WriteLine($"{locations.Count}x {category}");

                    foreach (var details in locations.ToLookup(i => i.details, i => i.filePosition).OrderByDescending(fps => fps.Count()))
                    {
                        textWriter.WriteLine("----------------------------------------------");

                        textWriter.WriteLine($"  {details.Count()}x {details.Key}");

                        foreach (var ps in details.ToLookup(fp => fp.File, fp => fp.Position).OrderByDescending(p => p.Count()))
                        {
                            textWriter.WriteLine($"    File: {ps.Key}");

                            foreach (var p in ps)
                            {
                                if (p.HasValue)
                                {
                                    textWriter.WriteLine($"      {p}");
                                }
                            }
                        }
                    }

                    textWriter.WriteLine("==============================================");
                }
            }
            else
            {
                textWriter.WriteLine("No problems found.");
            }
        }
    }

    public sealed class GithubActions : ProblemSummarizer
    {
        public override void WriteSummary(ProblemRecorder recorder, TextWriter textWriter)
        {
            if (recorder.Any)
            {
                var problems = recorder.GetRecords();

                textWriter.WriteLine();
                textWriter.WriteLine("Problems:");

                problems.Sort((x, y) => y.locations.Count - x.locations.Count);

                foreach (var (category, locations) in problems)
                {
                    textWriter.WriteLine();
                    textWriter.WriteLine($"::warning::{locations.Count}x `{category}`");

                    foreach (var details in locations.ToLookup(i => i.details, i => i.filePosition).OrderByDescending(fps => fps.Count()))
                    {
                        textWriter.WriteLine($"::group::{details.Count()}x <code>{details.Key}</code>");

                        foreach (var ps in details.ToLookup(fp => fp.File, fp => fp.Position).OrderByDescending(p => p.Count()))
                        {
                            textWriter.WriteLine($"File: {ps.Key.Replace('\\', '/')}");

                            foreach (var p in ps)
                            {
                                if (p.HasValue)
                                {
                                    textWriter.WriteLine($"\t{p}");
                                }
                            }
                        }

                        textWriter.WriteLine("::endgroup::");
                    }

                    textWriter.WriteLine($"::group::Show affected files");

                    foreach (var file in locations.Select(l => l.filePosition.File).Distinct())
                    {
                        textWriter.WriteLine($"- {file.Replace('\\', '/')}");
                    }

                    textWriter.WriteLine("::endgroup::");
                }
            }
            else
            {
                textWriter.WriteLine(":white_check_mark: No problems found.");
            }
        }
    }
}
