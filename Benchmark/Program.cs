using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Benchmark;

public static class Program
{
	static void Main(string[] args)
	{
		BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new MainConfig());
	}

	class MainConfig : ManualConfig
	{
		public MainConfig()
		{
			AddJob(Job.InProcess
#if DEBUG
				.WithWarmupCount(0)
				.WithUnrollFactor(2)
				.WithInvocationCount(2)
				.WithIterationCount(1));
#else
				.WithWarmupCount(2)
				.WithMinIterationCount(1)
				.WithMaxIterationCount(20));
#endif

			AddLogger(ConsoleLogger.Default)
				.AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams, BenchmarkLogicalGroupRule.ByCategory)
				.WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));

			AddColumnProvider(CustomColumnProviders.Instance);

			SummaryStyle = SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend);
		}
	}

	public class CustomColumnProviders : IColumnProvider
	{
		public static readonly IColumnProvider Instance = new CustomColumnProviders();

		public IEnumerable<IColumn> GetColumns(Summary summary)
		{
			if (summary.BenchmarksCases.Select(b => b.Descriptor.Categories).Distinct().Count() > 1)
				yield return CategoriesColumn.Default;

			if (summary.BenchmarksCases.Select(b => b.Descriptor.Type.Namespace).Distinct().Count() > 1)
				yield return TargetMethodColumn.Namespace;

			if (summary.BenchmarksCases.Select(b => b.Descriptor.Type.Name).Distinct().Count() > 1)
				yield return TargetMethodColumn.Type;

			yield return TargetMethodColumn.Method;

			foreach (var provider in DefaultColumnProviders.Job.GetColumns(summary))
				yield return provider;

			yield return StatisticColumn.Mean;
			yield return StatisticColumn.Error;

			foreach (var provider in DefaultColumnProviders.Params.GetColumns(summary))
				yield return provider;

			foreach (var provider in DefaultColumnProviders.Metrics.GetColumns(summary))
				yield return provider;

			yield return BaselineRatioColumn.RatioMean;
			yield return BaselineAllocationRatioColumn.RatioMean;
		}
	}
}