using System;

namespace RedBird.Abstractions.Hooks.Transaction;

public class HookHandle<THook> where THook : class, IHook
{
	public THook? Hook { get; private set; }

	public Exception? Failure { get; private set; }

	public ulong ResolvedAddress { get; private set; }

	public bool IsInstalled => Hook?.IsInstalled ?? false;

	public bool Success => Hook != null;

	public string Name { get; private set; } = "<unregistered>";

	public THook Require()
	{
		return Hook ?? throw new InvalidOperationException("Hook [" + Name + "] is not installed. " + ((Failure == null) ? "It was never registered or the transaction has not been committed." : ("Registration failed: " + Failure.Message)), Failure);
	}

	internal void Register(string name)
	{
		Name = name;
	}

	internal virtual void Attach(THook hook, ulong resolvedAddress)
	{
		Hook = hook;
		ResolvedAddress = resolvedAddress;
		Failure = null;
	}

	internal void Reject(Exception failure)
	{
		Hook = null;
		Failure = failure;
	}

	internal void Detach()
	{
		Hook = null;
		ResolvedAddress = 0uL;
	}
}
