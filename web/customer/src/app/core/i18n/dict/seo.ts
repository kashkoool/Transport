// SEO landing pages: /routes (routes hub), /bus/:route (route page), /city/:city (city hub).
// Keys are namespaced (seo.*) and merged into the main dictionary. Only VISIBLE page-body text is
// translated here — the <title>/meta/canonical/OG/JSON-LD stay English (prerendered for crawlers).
// `{n}`-style placeholders are filled by TranslationService.t(key, params).
export const seoDict: Record<'en' | 'ar', Record<string, string>> = {
  en: {
    // shared breadcrumbs / nav
    'seo.breadcrumb.home': 'Home',
    'seo.breadcrumb.routes': 'Routes',

    // routes-index (/routes)
    'seo.routes.title': 'Bus routes across Syria',
    'seo.routes.intro':
      'Browse every intercity bus route on TPX Travel. Compare operators, see fares from the cheapest available seat, and book your ticket online in under a minute.',
    'seo.routes.fromOrigin': 'From {origin}',
    'seo.routes.priceFrom': 'from {price} {currency}',

    // route-page (/bus/:route) — not found
    'seo.route.notFound.title': 'Route not found',
    'seo.route.notFound.body':
      "We couldn't find that route. Browse all available routes instead.",
    'seo.route.notFound.cta': 'See all routes',

    // route-page — main
    'seo.route.h1': 'Bus from {origin} to {destination}',
    'seo.route.priceFrom': 'From {price} {currency}',
    'seo.route.durationApprox': '~{duration}',
    'seo.route.operatorsCount': '{count} operator',
    'seo.route.operatorsCountPlural': '{count} operators',
    'seo.route.intro':
      'Book your bus ticket from {origin} to {destination} online with TPX Travel. Compare every operator on this route, choose your exact seat on a live seat map, and pay securely to get an instant QR ticket. Fares start {price}.',
    'seo.route.intro.priceFrom': 'from {price} {currency}',
    'seo.route.intro.priceCompetitive': 'at competitive rates',
    'seo.route.searchCta': 'Search {origin} → {destination} buses',
    'seo.route.nextDepartures': 'Next departures',
    'seo.route.arrives': 'arrives {time}',
    'seo.route.operators': 'Bus operators on this route',
    'seo.route.faq': 'Frequently asked questions',
    'seo.route.closingTitle': 'Ready to travel {origin} → {destination}?',
    'seo.route.closingBody':
      "Pick your date and seat now — you'll have a QR ticket in under a minute.",
    'seo.route.closingCta': 'Find your trip',

    // route-page — FAQ (built in TS)
    'seo.route.faq.priceQ': 'How much is a bus ticket from {origin} to {destination}?',
    'seo.route.faq.priceA':
      'Tickets on the {origin} to {destination} route start {price}. The exact fare depends on the operator, bus type and how early you book.',
    'seo.route.faq.price.from': 'from {price} {currency}',
    'seo.route.faq.price.competitive': 'at competitive fares',
    'seo.route.faq.durationQ': 'How long does the {origin} to {destination} bus take?',
    'seo.route.faq.durationA':
      'The journey takes about {duration} on average, depending on the operator and road conditions.',
    'seo.route.faq.durationAUnknown':
      'Journey time varies by operator and road conditions. Exact durations are shown for each departure when you search.',
    'seo.route.faq.seatQ': 'Can I choose my seat and pay online?',
    'seo.route.faq.seatA':
      'Yes. TPX Travel shows a live seat map for every bus so you pick your exact seat, then pay securely online and receive an instant QR ticket.',
    'seo.route.faq.origin': 'the origin',
    'seo.route.faq.destination': 'the destination',

    // city-page (/city/:city)
    'seo.city.notFound.title': 'City not found',
    'seo.city.notFound.body': 'Browse all available routes instead.',
    'seo.city.notFound.cta': 'See all routes',
    'seo.city.h1': 'Bus routes from {city}',
    'seo.city.intro':
      'Find and book intercity buses departing {city}. Compare operators and fares, pick your seat, and get an instant QR ticket with TPX Travel.',
    'seo.city.empty': 'No routes from this city yet.',
    'seo.city.priceFrom': 'from {price} {currency}',
  },
  ar: {
    // shared breadcrumbs / nav
    'seo.breadcrumb.home': 'الرئيسية',
    'seo.breadcrumb.routes': 'الخطوط',

    // routes-index (/routes)
    'seo.routes.title': 'خطوط الباص عبر سوريا',
    'seo.routes.intro':
      'تصفّح كل خطوط الباص بين المدن على تي بي إكس ترافل. قارن بين الشركات، اطّلع على الأسعار بدءًا من أرخص مقعد متاح، واحجز تذكرتك عبر الإنترنت في أقل من دقيقة.',
    'seo.routes.fromOrigin': 'من {origin}',
    'seo.routes.priceFrom': 'بدءًا من {price} {currency}',

    // route-page (/bus/:route) — not found
    'seo.route.notFound.title': 'الخط غير موجود',
    'seo.route.notFound.body': 'تعذّر العثور على هذا الخط. تصفّح كل الخطوط المتاحة بدلًا من ذلك.',
    'seo.route.notFound.cta': 'عرض كل الخطوط',

    // route-page — main
    'seo.route.h1': 'الباص من {origin} إلى {destination}',
    'seo.route.priceFrom': 'بدءًا من {price} {currency}',
    'seo.route.durationApprox': '~{duration}',
    'seo.route.operatorsCount': 'شركة واحدة',
    'seo.route.operatorsCountPlural': '{count} شركات',
    'seo.route.intro':
      'احجز تذكرة الباص من {origin} إلى {destination} عبر الإنترنت مع تي بي إكس ترافل. قارن بين كل الشركات على هذا الخط، اختر مقعدك بالتحديد على خريطة مقاعد حية، وادفع بأمان لتحصل على تذكرة QR فورية. تبدأ الأسعار {price}.',
    'seo.route.intro.priceFrom': 'من {price} {currency}',
    'seo.route.intro.priceCompetitive': 'بأسعار تنافسية',
    'seo.route.searchCta': 'ابحث عن باصات {origin} ← {destination}',
    'seo.route.nextDepartures': 'الرحلات القادمة',
    'seo.route.arrives': 'الوصول {time}',
    'seo.route.operators': 'شركات الباص على هذا الخط',
    'seo.route.faq': 'الأسئلة الشائعة',
    'seo.route.closingTitle': 'جاهز للسفر من {origin} إلى {destination}؟',
    'seo.route.closingBody': 'اختر تاريخك ومقعدك الآن — ستحصل على تذكرة QR في أقل من دقيقة.',
    'seo.route.closingCta': 'اعثر على رحلتك',

    // route-page — FAQ (built in TS)
    'seo.route.faq.priceQ': 'كم سعر تذكرة الباص من {origin} إلى {destination}؟',
    'seo.route.faq.priceA':
      'تبدأ التذاكر على خط {origin} إلى {destination} {price}. يعتمد السعر الدقيق على الشركة ونوع الباص ومدى تبكيرك في الحجز.',
    'seo.route.faq.price.from': 'من {price} {currency}',
    'seo.route.faq.price.competitive': 'بأسعار تنافسية',
    'seo.route.faq.durationQ': 'كم تستغرق رحلة الباص من {origin} إلى {destination}؟',
    'seo.route.faq.durationA':
      'تستغرق الرحلة نحو {duration} في المتوسط، حسب الشركة وحالة الطريق.',
    'seo.route.faq.durationAUnknown':
      'يختلف زمن الرحلة حسب الشركة وحالة الطريق. تُعرض المدة الدقيقة لكل رحلة عند البحث.',
    'seo.route.faq.seatQ': 'هل يمكنني اختيار مقعدي والدفع عبر الإنترنت؟',
    'seo.route.faq.seatA':
      'نعم. يعرض تي بي إكس ترافل خريطة مقاعد حية لكل باص لتختار مقعدك بالتحديد، ثم تدفع بأمان عبر الإنترنت وتحصل على تذكرة QR فورية.',
    'seo.route.faq.origin': 'المدينة المنطلق منها',
    'seo.route.faq.destination': 'المدينة الوجهة',

    // city-page (/city/:city)
    'seo.city.notFound.title': 'المدينة غير موجودة',
    'seo.city.notFound.body': 'تصفّح كل الخطوط المتاحة بدلًا من ذلك.',
    'seo.city.notFound.cta': 'عرض كل الخطوط',
    'seo.city.h1': 'خطوط الباص من {city}',
    'seo.city.intro':
      'اعثر واحجز باصات بين المدن تنطلق من {city}. قارن بين الشركات والأسعار، اختر مقعدك، واحصل على تذكرة QR فورية مع تي بي إكس ترافل.',
    'seo.city.empty': 'لا توجد خطوط من هذه المدينة بعد.',
    'seo.city.priceFrom': 'من {price} {currency}',
  },
};
