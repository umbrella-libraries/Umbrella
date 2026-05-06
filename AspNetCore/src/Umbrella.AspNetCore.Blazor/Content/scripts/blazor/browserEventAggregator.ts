/**
 * Aggregates browser event subscriptions and dispatches event notifications to .NET listeners.
 */
export class BrowserEventAggregator
{
	#subscriptionMap = new Map<string, BrowserEventSubscription[]>();
	#eventHandlerMap = new Map<string, () => Promise<void>>();

	/**
	 * Adds a subscription for a browser event and attaches a shared window event handler when needed.
	 * If a subscription with the same id already exists for the event it is replaced in-place.
	 * @param id A unique subscription identifier.
	 * @param eventName The browser event name to subscribe to.
	 * @param dotNetObjectReference The .NET object reference that receives event callbacks.
	 */
	public addEventListener(id: string, eventName: string, dotNetObjectReference: DotNetObjectReference): void
	{
		let subscriptions = this.#subscriptionMap.get(eventName);

		if (!subscriptions)
		{
			subscriptions = new Array<BrowserEventSubscription>();
			this.#subscriptionMap.set(eventName, subscriptions);

			let eventHandler = this.#eventHandlerMap.get(eventName);

			if (!eventHandler)
			{
				eventHandler = this.notifyEventSubscribersAsync.bind(this, eventName);
				this.#eventHandlerMap.set(eventName, eventHandler);
			}

			window.addEventListener(eventName, eventHandler);
		}

		const existingIndex = subscriptions.findIndex(x => x.id === id);

		if (existingIndex >= 0)
			subscriptions[existingIndex] = new BrowserEventSubscription(id, eventName, dotNetObjectReference);
		else
			subscriptions.push(new BrowserEventSubscription(id, eventName, dotNetObjectReference));
	}

	/**
	 * Removes a subscription for a browser event and detaches the window handler when no listeners remain.
	 * @param id The unique subscription identifier.
	 * @param eventName The browser event name to unsubscribe from.
	 */
	public removeEventListener(id: string, eventName: string): void
	{
		const subscriptions = this.#subscriptionMap.get(eventName);

		if (!subscriptions)
			return;

		const subscription = subscriptions.find(x => x.id === id);

		if (subscription)
		{
			const updatedSubscriptions = subscriptions.filter(x => x !== subscription);

			if (updatedSubscriptions.length > 0)
			{
				this.#subscriptionMap.set(eventName, updatedSubscriptions);
			}
			else
			{
				this.#subscriptionMap.delete(eventName);

				const eventHandler = this.#eventHandlerMap.get(eventName);

				if (eventHandler)
					window.removeEventListener(eventName, eventHandler);

				this.#eventHandlerMap.delete(eventName);
			}
		}
	}

	/**
	 * Notifies all subscribers for a given browser event.
	 * @param eventName The browser event name being dispatched.
	 */
	private async notifyEventSubscribersAsync(eventName: string): Promise<void>
	{
		const subscriptions = this.#subscriptionMap.get(eventName);

		if (subscriptions)
		{
			for (let item of subscriptions)
			{
				await item.publishAsync();
			}
		}
	}
}

/**
 * Represents a single browser event subscription bound to a .NET callback target.
 */
class BrowserEventSubscription
{
	/**
	 * Creates a new browser event subscription.
	 * @param id A unique subscription identifier.
	 * @param eventName The browser event name associated with this subscription.
	 * @param dotNetObjectReference The .NET object reference used for publishing event callbacks.
	 */
	constructor(public id: string, public eventName: string, private dotNetObjectReference: DotNetObjectReference)
	{
	}

	/**
	 * Publishes this subscription event to the associated .NET object reference.
	 */
	public async publishAsync(): Promise<void>
	{
		await this.dotNetObjectReference.invokeMethodAsync("PublishAsync", this.eventName);
	}
}

declare type DotNetObjectReference = {
	invokeMethodAsync: (methodName: string, ...args: any) => Promise<void>
}