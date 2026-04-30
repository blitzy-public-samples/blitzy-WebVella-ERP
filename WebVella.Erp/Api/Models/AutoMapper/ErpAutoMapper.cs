using AutoMapper;
using AutoMapper.Configuration;
using AutoMapper.Internal;
using System;
using System.Collections.Generic;
using System.Text;

namespace WebVella.Erp.Api.Models.AutoMapper
{
	public static class ErpAutoMapper
	{
		// CVE-2026-32933 / GHSA-rvv3-g6hj-g44x mitigation depth limit.
		// Matches the default introduced in AutoMapper 15.1.1+ (and System.Text.Json /
		// Newtonsoft.Json) for the same DoS class.
		private const int DefaultMaxDepth = 64;

		public static IMapper Mapper = null;

		public static void Initialize(MapperConfigurationExpression cfg)
		{
			// CVE-2026-32933 / GHSA-rvv3-g6hj-g44x defensive mitigation.
			// AutoMapper v14 (the last MIT-licensed line) does not enforce a default
			// recursion-depth limit on object-graph mapping. A specially crafted, deeply
			// nested object graph submitted to any Map<>() call site can therefore exhaust
			// the calling thread's stack and trigger a StackOverflowException, terminating
			// the host process. Upstream's fix in v15.1.1+ applies a default MaxDepth of 64
			// to every type map; v14 has no built-in equivalent.
			//
			// This callback registers the same hardening at solution scope: it runs once
			// during MapperConfiguration build (after every plugin profile has been added
			// via SetAutoMapperConfiguration) and applies MaxDepth(64) to every type map
			// that has not declared an explicit depth via .MaxDepth(...). Maps that need a
			// larger depth retain their explicit configuration unchanged.
			cfg.Internal().ForAllMaps((typeMap, mappingExpression) =>
			{
				if (typeMap.MaxDepth == 0)
				{
					mappingExpression.MaxDepth(DefaultMaxDepth);
				}
			});
			Mapper = new Mapper(new MapperConfiguration(cfg));
		}
	}
}
