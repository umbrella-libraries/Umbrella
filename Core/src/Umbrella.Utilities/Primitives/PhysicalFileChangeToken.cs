using Microsoft.Extensions.Primitives;

namespace Umbrella.Utilities.Primitives;

/// <summary>
/// An <see cref="IChangeToken"/> implementation that watches for changes to files
/// on disk using the <see cref="FileSystemWatcher" />.
/// </summary>
/// <seealso cref="IChangeToken" />
public class PhysicalFileChangeToken : IChangeToken
{
	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="PhysicalFileChangeToken"/> class.
	/// </summary>
	/// <param name="fileInfo">The file information.</param>
	/// <param name="notifyOnChanged">if set to <see langword="true" />, raises a notification when the file is changed.</param>
	/// <param name="notifyOnError">if set to <see langword="true" />, raises a notification when the <see cref="FileSystemWatcher"/> is no longer able to watch the file.</param>
	/// <param name="notifyOnDeleted">if set to <see langword="true" />, raises a notification when the file is deleted.</param>
	/// <param name="notifyOnRenamed">if set to <see langword="true" />, raises a notification when the file is renamed.</param>
	public PhysicalFileChangeToken(
		FileInfo fileInfo,
		bool notifyOnChanged = true,
		bool notifyOnError = true,
		bool notifyOnDeleted = true,
		bool notifyOnRenamed = true)
		: this(GetDirectoryName(fileInfo), GetFileName(fileInfo), notifyOnChanged, notifyOnError, notifyOnDeleted, notifyOnRenamed)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PhysicalFileChangeToken"/> class.
	/// </summary>
	/// <param name="directoryPath">The directory path.</param>
	/// <param name="fileName">Name of the file.</param>
	/// <param name="notifyOnChanged">if set to <see langword="true" />, raises a notification when the file is changed.</param>
	/// <param name="notifyOnError">if set to <see langword="true" />, raises a notification when the <see cref="FileSystemWatcher"/> is no longer able to watch the file.</param>
	/// <param name="notifyOnDeleted">if set to <see langword="true" />, raises a notification when the file is deleted.</param>
	/// <param name="notifyOnRenamed">if set to <see langword="true" />, raises a notification when the file is renamed.</param>
	public PhysicalFileChangeToken(
		string directoryPath,
		string fileName,
		bool notifyOnChanged = true,
		bool notifyOnError = true,
		bool notifyOnDeleted = true,
		bool notifyOnRenamed = true)
	{
#pragma warning disable CA2000 // Dispose objects before losing scope
		var fsw = new FileSystemWatcher(directoryPath, fileName);
#pragma warning restore CA2000 // Dispose objects before losing scope

		if (notifyOnChanged)
			fsw.Changed += (sender, args) => HasChanged = true;

		if (notifyOnError)
			fsw.Error += (sender, args) => HasChanged = true;

		if (notifyOnDeleted)
			fsw.Deleted += (sender, args) => HasChanged = true;

		if (notifyOnRenamed)
			fsw.Renamed += (sender, args) => HasChanged = true;

		fsw.EnableRaisingEvents = true;
	}
	#endregion

	#region IChangeToken Members
	/// <inheritdoc />
	public bool HasChanged { get; set; }

	/// <inheritdoc />
	public bool ActiveChangeCallbacks => false;

	/// <inheritdoc />
	public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;
	#endregion

	private static string GetDirectoryName(FileInfo fileInfo)
	{
		CommunityToolkit.Diagnostics.Guard.IsNotNull(fileInfo);

		return fileInfo.DirectoryName ?? "";
	}

	private static string GetFileName(FileInfo fileInfo)
	{
		CommunityToolkit.Diagnostics.Guard.IsNotNull(fileInfo);

		return fileInfo.Name;
	}
}
