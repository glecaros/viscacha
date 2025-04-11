using System.Collections.Generic;

namespace Viscacha.Model;

public record Document(Defaults? Defaults, List<Request> Requests);
