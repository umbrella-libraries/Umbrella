using System.Data;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for <see cref="IDbConnection"/> instances.
/// </summary>
public static class IDbConnectionExtensions
{
	/// <summary>
	/// Ensures the provided database connection is open.
	/// </summary>
	/// <param name="dbConnection">The database connection.</param>
	/// <returns>The database connection.</returns>
	public static IDbConnection EnsureOpened(this IDbConnection dbConnection)
	{
		Guard.IsNotNull(dbConnection);

		// There is a small possibility of a race condition here where 2 threads could attempt to open the same connection at once.
		// Locking on the connection object to guard against this.
		if (dbConnection.State == ConnectionState.Closed)
		{
			lock (dbConnection)
			{
				if (dbConnection.State == ConnectionState.Closed)
				{
					dbConnection.Open();
				}
			}
		}

		return dbConnection;
	}
}