using System.Collections.Generic;
using Core;
using Core.Save;

public readonly struct CurrencyChangedEvent
{
    public readonly string Currency;
    public readonly long Amount;
    public CurrencyChangedEvent(string currency, long amount)
    {
        Currency = currency;
        Amount = amount;
    }
}

public sealed class CurrencyService : ISavable
{
    public string SaveKey => "warm";

    Dictionary<string, long> _balances = new Dictionary<string, long>();
    readonly SaveService _save;
    readonly IEventBus _bus;

    public CurrencyService(SaveService save, IEventBus bus)
    {
        _save = save;
        _bus = bus;
        _save.Register(this);
    }

    public long Get(string currency)
        => _balances.TryGetValue(currency, out var v) ? v : 0;

    public void Add(string currency, long amount)
    {
        if (amount == 0) return;
        _balances[currency] = Get(currency) + amount;
        Changed(currency);
    }

    public bool TrySpend(string currency, long amount)
    {
        if (amount < 0 || Get(currency) < amount) return false;
        _balances[currency] = Get(currency) - amount;
        Changed(currency);
        return true;
    }

    void Changed(string currency)
    {
        _bus.Publish(new CurrencyChangedEvent(currency, Get(currency)));
        _save.Save(SaveKey);
    }

    public void Save(SaveBag bag) => bag.Set("Currency", _balances);

    public void Load(SaveBag bag) => _balances = bag.Get("Currency", _balances);
}
