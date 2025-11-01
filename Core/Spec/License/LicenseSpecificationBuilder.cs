using System.Linq.Expressions;
using Core.Entities;
using Core.Helpers;

namespace Core.Spec;

public static class LicenseSpecificationBuilder
{
	public static Expression<Func<License, bool>> Build(LicenseSpecParams specParams)
	{
		var statusEnum = LicenseStatusHelper.Parse(specParams.Status);

		return x =>
		   (!specParams.UserId.HasValue || x.UserId == specParams.UserId) &&
		   (!specParams.CreatedAfter.HasValue || x.CreatedAt >= specParams.CreatedAfter.Value) &&
		   (!specParams.CreatedBefore.HasValue || x.CreatedAt <= specParams.CreatedBefore.Value) &&
		   (!statusEnum.HasValue || x.Status == statusEnum.Value) &&
		   (string.IsNullOrEmpty(specParams.Search) ||
			  x.LicenseKey!.ToLower().Contains(specParams.Search));
	}
}
