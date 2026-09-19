                                                               

                       
                 
                 
                  
           
                 

                                   
                  
             
                                          
   

export class LocalEventLog {
                   storage             ;
                   key        ;
                   now              ;

  constructor(
    storage             ,
    key = 'phase-ripple-events-v1',
    now               = () => Date.now(),
  ) {
    this.storage = storage;
    this.key = key;
    this.now = now;
  }

  read()               {
    try {
      const stored = this.storage.getItem(this.key);
      if (stored === null) return [];
      const value = JSON.parse(stored);
      return Array.isArray(value) ? value : [];
    } catch {
      return [];
    }
  }

  record(type           , data                                   )             {
    const event             = { type, at: this.now(), data };
    const events = [...this.read(), event].slice(-500);
    this.storage.setItem(this.key, JSON.stringify(events));
    return event;
  }
}

export function createMemoryStorage()              {
  const values = new Map                ();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => { values.set(key, value); },
  };
}
