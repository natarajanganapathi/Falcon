# Builder Pattern C# Example

```C#
public class Person
{
    public string FirstName { get; private set; }
    public string? MiddleName { get; private set; }
    public string LastName { get; private set; }
    public int Age { get; private set; }

    private Person() { }

    public class Builder
    {
        private readonly Person _person = new Person();

        public Builder SetFirstName(string firstName)
        {
            _person.FirstName = firstName;
            return this;
        }

        public Builder SetMiddleName(string? middleName)
        {
            _person.MiddleName = middleName;
            return this;
        }

        public Builder SetLastName(string lastName)
        {
            _person.LastName = lastName;
            return this;
        }

        public Builder SetAge(int age)
        {
            _person.Age = age;
            return this;
        }

        public Person Build()
        {
            // Ensure required properties are set, if any
            if (string.IsNullOrWhiteSpace(_person.FirstName))
                throw new InvalidOperationException("First name is required");

            if (string.IsNullOrWhiteSpace(_person.LastName))
                throw new InvalidOperationException("Last name is required");

            return _person;
        }
    }
}

// Usage
var person = new Person.Builder()
    .SetFirstName("John")
    .SetLastName("Doe")
    .SetAge(30)
    .Build();

```
